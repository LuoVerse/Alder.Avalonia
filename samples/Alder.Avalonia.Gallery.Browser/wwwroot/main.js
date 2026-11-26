try {
    const { dotnet } = await import("./_framework/dotnet.js");
    const { getConfig, runMain } = await dotnet.create();
    // Avalonia continues to render and process input after the entry point returns.
    await runMain(getConfig().mainAssemblyName, []);
    document.getElementById("startup-status")?.remove();
} catch (error) {
    console.error("Alder Gallery could not start.", error);
    const status = document.getElementById("startup-status");
    if (status) {
        status.textContent = "Alder Gallery could not start. Refresh the page to try again.";
    }
}
