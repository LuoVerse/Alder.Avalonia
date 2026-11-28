# AI conversations and Markdown

**English** · [简体中文](chat-and-markdown.zh-CN.md)

Use `AlderMarkdown` to render Markdown and `AlderChatConversation` to organize a scrollable conversation. The host application handles sending messages, calling models, approving tool calls, and uploading attachments. For editable rich text, see the editor guide. [Rich Text Editor](rich-text-editor.md)

## Static content and streaming responses

Bind completed Markdown directly through `Markdown`. To show source code, pass the source and language name to `AlderCodeBlock.Code` and `Language`.

```xml
<alder:AlderMarkdown Markdown="{CompiledBinding ReplyText}" />
```

Call `BeginStream()` on the UI thread to start a streaming response. Forward incoming text deltas to `Push` and call `Complete` at the end. Collect the original text in the application for persistence and restoration.

```csharp
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alder.Avalonia.Controls;

static async Task<string> DisplayReplyAsync(AlderMarkdown view,
    IAsyncEnumerable<string> chunks, CancellationToken cancellationToken)
{
    var stream = view.BeginStream();
    var body = new StringBuilder();
    try
    {
        await foreach (var delta in chunks.WithCancellation(cancellationToken))
        {
            body.Append(delta);
            stream.Push(delta);
        }
        return body.ToString();
    }
    finally
    {
        stream.Complete();
    }
}
```

You can call `Push` and `Complete` from a background thread. The function above returns the full Markdown on success and propagates cancellation and exceptions to the caller. To keep an incomplete response, incrementally store its body in the host's message model along with the cancellation or failure status. If the stream sends cumulative text, extract the new portion before pushing it. [Markdown example](../../samples/Alder.Avalonia.Gallery/Views/MarkdownExample.axaml) · [Streaming and source code ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/AiExampleViewModel.cs)

## Saving and reopening chat history

Bind `AlderChatConversation.ItemsSource` to an `ObservableCollection<TranscriptEntry>`. Use `MarkdownEntry.FromText` for Markdown-only responses. Use `ContentEntry` to wrap controls for messages that include avatars, actions, or cards. Add, replace, and clear bound entries on the UI thread.

Save your own conversation records with fields such as `id`, `role`, Markdown body, message order, attachment storage IDs, and tool state. This example rebuilds bindable entries from the loaded messages.

```csharp
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Alder.Avalonia.Controls;
using Alder.NativeMarkdown.UI.Transcript;

public sealed record SavedMessage(string Id, bool IsUser, string Body);

public static class TranscriptFactory
{
    public static ObservableCollection<TranscriptEntry> Restore(IEnumerable<SavedMessage> history)
    {
        var entries = new ObservableCollection<TranscriptEntry>();
        foreach (var message in history)
        {
            if (message.IsUser)
                entries.Add(new ContentEntry(new AlderChatMessage
                {
                    IsUser = true,
                    Content = message.Body
                }));
            else
                entries.Add(MarkdownEntry.FromText(message.Body, tag: message.Id));
        }
        return entries;
    }
}
```

For a new response, you can add a `MarkdownEntry` backed by a `MarkdownStream` and then push deltas. Use `ShowScrollButton` to offer a way back to the bottom and `ScrollOnUserMessage` to control scrolling after a message is sent. When reopening a completed conversation, build static entries from the saved message bodies and tool states. [Conversation example](../../samples/Alder.Avalonia.Gallery/Views/ChatConversationExample.axaml) · [C# for sending and rebuilding entries](../../samples/Alder.Avalonia.Gallery/ViewModels/AiExampleViewModel.cs)

## Prompt input, models, tools, and attachments

| Component | Integration and restoration |
| --- | --- |
| Prompt Input | Bind `Text` two-way. Use `SendCommand` to create a request from the draft, `StopCommand` to cancel, and `IsRunning` to reflect request state. `Tools` and `Attachments` are content slots. Restore unsent drafts and saved attachment references when reopening. [Example](../../samples/Alder.Avalonia.Gallery/Views/PromptInputExample.axaml) |
| Model Selector | Bind `ItemsSource`, `Efforts`, `SelectedModel`, and `SelectedEffort`. Persist the application's model ID and reasoning effort, then restore the selection after refreshing the available models. [Example](../../samples/Alder.Avalonia.Gallery/Views/ModelSelectorExample.axaml) |
| Chat Tool, Approval Card | Set `AlderChatTool.Status`, `Arguments`, and `Result` from application state. Bind the approval card's `ApproveCommand`, `RejectCommand`, and `Decision`. Save the tool call ID, result, and decision. Restore the recorded state and leave any follow-up execution to the application's conversation logic. [Tool example](../../samples/Alder.Avalonia.Gallery/Views/ChatToolExample.axaml) · [Approval card example](../../samples/Alder.Avalonia.Gallery/Views/ApprovalCardExample.axaml) |
| Chat Attachment, Chat Source | Create attachment cards from uploaded file records or reusable file references. Create source cards from titles and URLs. The host reads, writes, and uploads files supplied through the picker or drag and drop. [Attachment example](../../samples/Alder.Avalonia.Gallery/Views/ChatAttachmentExample.axaml) · [Source example](../../samples/Alder.Avalonia.Gallery/Views/ChatSourceExample.axaml) |
| Chat List View, Chat Message Actions | Bind the list to the application's conversation model and bind each message action to its command. Persist conversation and message IDs and use the current collection to restore selection and command context. [List example](../../samples/Alder.Avalonia.Gallery/Views/ChatListViewExample.axaml) · [Actions example](../../samples/Alder.Avalonia.Gallery/Views/ChatMessageActionsExample.axaml) |
| Reasoning, Chain of Thought, Loader | Render user-facing summaries, steps, and progress from application records. Rebuild the display from saved state when loading history and keep the loading indicator in sync with the current request. [Summary example](../../samples/Alder.Avalonia.Gallery/Views/ReasoningExample.axaml) · [Steps example](../../samples/Alder.Avalonia.Gallery/Views/ChainOfThoughtExample.axaml) · [Loading example](../../samples/Alder.Avalonia.Gallery/Views/ChatLoaderExample.axaml) |
