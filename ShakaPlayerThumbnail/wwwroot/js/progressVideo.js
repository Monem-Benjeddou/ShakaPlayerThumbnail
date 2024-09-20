const connection = new signalR.HubConnectionBuilder()
    .withUrl("/uploadProgressHub", { transport: signalR.HttpTransportType.WebSockets })
    .configureLogging(signalR.LogLevel.Information)
    .build();


connection.on("ReceiveProgress", (videoName, progress) => {
    let encodedVideoName = encodeURIComponent(videoName);
    const progressBar = document.getElementById(`progress-${encodedVideoName}`);

    if (progressBar) {
        progressBar.style.width = progress + '%';
        progressBar.setAttribute('aria-valuenow', progress);
        progressBar.textContent = progress + '%';

        if (progress === 100) {
            const displayButton = document.getElementById(`btn-display-${encodedVideoName}`);
            if (displayButton) {
                displayButton.classList.remove('disabled');
            }
        }
    }
});

connection.start().catch(err => console.error(err.toString()));