// Establish connection to the SignalR hub
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/uploadProgressHub", { transport: signalR.HttpTransportType.WebSockets })
    .configureLogging(signalR.LogLevel.Information)
    .withAutomaticReconnect() 
    .build();

connection.on("ReceiveProgress", (videoName, progress) => {
    let encodedVideoName = encodeURIComponent(videoName);
    const progressBar = document.getElementById(`progress-${encodedVideoName}`);
    const createChaptersButton = document.getElementById(`btn-create-chapters-${encodedVideoName}`);
    const displayButton = document.getElementById(`btn-display-${encodedVideoName}`);
    const statusElement = document.querySelector(`#video-row-${encodedVideoName} .badge`);

    if (progressBar) {
        progressBar.style.width = progress + '%';
        progressBar.setAttribute('aria-valuenow', progress);
        progressBar.textContent = progress + '%';

        if (progress === 100) {
            progressBar.parentElement.style.display = 'none';

            if (statusElement) {
                statusElement.textContent = "Processing complete";
                statusElement.classList.remove("bg-warning", "text-dark");
                statusElement.classList.add("bg-success");
            }

            if (createChaptersButton) {
                createChaptersButton.classList.remove('disabled');
            }
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
        // Calculate hours, minutes, and seconds
        let hours = Math.floor(taskTime / 3600);
        let minutes = Math.floor((taskTime % 3600) / 60);
        let seconds = Math.floor(taskTime % 60);

        // Update the task duration in hh:mm:ss format
        durationBar.textContent = `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
    }
});

// Start the SignalR connection and handle errors
connection.start().then(() => {
    console.log("Connected to SignalR hub.");
}).catch(err => console.error("Error connecting to SignalR hub: ", err.toString()));

connection.onreconnecting((error) => {
    console.warn("Connection lost. Reconnecting...");
});

connection.onreconnected((connectionId) => {
    console.log("Connection reestablished. Connected with connectionId: " + connectionId);
});

connection.onclose((error) => {
    console.error("Connection closed due to error: ", error);
});
