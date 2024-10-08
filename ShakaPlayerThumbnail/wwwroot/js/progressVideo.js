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

connection.on("ReceiveTaskTime", (videoName, taskTime) => {
    let encodedVideoName = encodeURIComponent(videoName);
    const durationBar = document.getElementById(`duration-${encodedVideoName}`);

    if (durationBar) {
        // Calculate hours, minutes, and seconds from taskTime
        let hours = Math.floor(taskTime / 3600);
        let minutes = Math.floor((taskTime % 3600) / 60);
        let seconds = Math.floor(taskTime % 60);

        durationBar.textContent = `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
    }
});

connection.start().catch(err => console.error(err.toString()));