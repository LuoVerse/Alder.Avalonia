# AI 会话与 Markdown

用 `AlderMarkdown` 渲染 Markdown 文稿，用 `AlderChatConversation` 组织可滚动的会话。消息发送、模型调用、工具批准和附件上传由宿主实现。需要带格式的可编辑正文时，参见 [Rich Text Editor](rich-text-editor.md)。

## 静态内容与流式响应

已经完整的 Markdown 可直接绑定 `Markdown`。有代码源码时，`AlderCodeBlock.Code` 和 `Language` 可以直接接收源代码和语言名。

```xml
<alder:AlderMarkdown Markdown="{CompiledBinding ReplyText}" />
```

流式响应开始时在 UI 线程调用 `BeginStream()`，之后把收到的新增文本传给 `Push`，结束时调用 `Complete`。同时在应用侧收集原始文本，用于保存和后续恢复。

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

`Push` 和 `Complete` 可由后台线程调用。上面的函数成功时返回可保存的完整 Markdown，取消和异常交给调用方。需要保留未完成回复时，在宿主的消息模型里增量记录正文，并附上取消或失败状态。若流式请求发来的是累计全文，先提取新增部分再推送。[Markdown 示例](../../samples/Alder.Avalonia.Gallery/Views/MarkdownExample.axaml) · [流式与代码示例 ViewModel](../../samples/Alder.Avalonia.Gallery/ViewModels/AiExampleViewModel.cs)

## 保存与重开聊天记录

给 `AlderChatConversation.ItemsSource` 绑定 `ObservableCollection<TranscriptEntry>`。纯 Markdown 回复可用 `MarkdownEntry.FromText` 创建。包含头像、操作或卡片的消息可用 `ContentEntry` 包装控件。在 UI 线程添加、替换或清空绑定集合。

保存自己的会话记录，例如 `id`、`role`、正文 Markdown、消息顺序、附件存储 ID 和工具状态。以下示例从已加载的消息集合重建可绑定条目。

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

创建新回复时，也可为会话添加一个由 `MarkdownStream` 创建的 `MarkdownEntry`，随后推送增量。可用 `ShowScrollButton` 提供返回底部入口，用 `ScrollOnUserMessage` 控制发送消息后的跟随行为。打开已完成的历史记录时直接用静态条目重建，正文和工具状态均从持久化记录取得。[会话示例](../../samples/Alder.Avalonia.Gallery/Views/ChatConversationExample.axaml) · [发送与重建条目的 C# 代码](../../samples/Alder.Avalonia.Gallery/ViewModels/AiExampleViewModel.cs)

## 组合输入、模型、工具与附件

| 组件 | 接入及恢复 |
| --- | --- |
| Prompt Input | 双向绑定 `Text`，用 `SendCommand` 从草稿创建请求，用 `StopCommand` 取消，用 `IsRunning` 映射运行状态。`Tools` 和 `Attachments` 是界面插槽。重新打开时可恢复未发送草稿和已保存的附件引用。[示例](../../samples/Alder.Avalonia.Gallery/Views/PromptInputExample.axaml) |
| Model Selector | 绑定 `ItemsSource`、`Efforts`、`SelectedModel` 和 `SelectedEffort`。保存应用的模型 ID 和推理强度，刷新可用模型后重新选择。[示例](../../samples/Alder.Avalonia.Gallery/Views/ModelSelectorExample.axaml) |
| Chat Tool、Approval Card | 从应用状态设置 `AlderChatTool.Status`、`Arguments` 和 `Result`。批准卡绑定 `ApproveCommand`、`RejectCommand` 和 `Decision`。保存工具调用 ID、结果以及决定。恢复时展示已记录状态，后续执行由应用的会话逻辑决定。[工具示例](../../samples/Alder.Avalonia.Gallery/Views/ChatToolExample.axaml) · [批准卡示例](../../samples/Alder.Avalonia.Gallery/Views/ApprovalCardExample.axaml) |
| Chat Attachment、Chat Source | 从已上传附件的记录或可重新访问的文件引用构建卡片，从标题和地址构建来源。文件选择器或拖放只提供输入，宿主负责读写与上传。[附件示例](../../samples/Alder.Avalonia.Gallery/Views/ChatAttachmentExample.axaml) · [来源示例](../../samples/Alder.Avalonia.Gallery/Views/ChatSourceExample.axaml) |
| Chat List View、Chat Message Actions | 会话列表绑定应用的会话模型，消息操作绑定对应命令。保存会话和消息 ID，打开时从当前集合恢复选择和命令上下文。[列表示例](../../samples/Alder.Avalonia.Gallery/Views/ChatListViewExample.axaml) · [操作示例](../../samples/Alder.Avalonia.Gallery/Views/ChatMessageActionsExample.axaml) |
| Reasoning、Chain of Thought、Loader | 从业务记录渲染可向用户展示的摘要、步骤和过程反馈。加载历史时根据已有状态重建内容，运行中的加载指示随当前请求更新。[摘要示例](../../samples/Alder.Avalonia.Gallery/Views/ReasoningExample.axaml) · [步骤示例](../../samples/Alder.Avalonia.Gallery/Views/ChainOfThoughtExample.axaml) · [加载示例](../../samples/Alder.Avalonia.Gallery/Views/ChatLoaderExample.axaml) |
