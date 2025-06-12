document.addEventListener('DOMContentLoaded', () => {
    const audioPlayer = document.getElementById('audioPlayer');
    const fileListDiv = document.getElementById('fileList');
    let files = [];
    let currentFileIndex = 0;

    const statusMessage = document.getElementById('status-message');
    const speakerName = document.getElementById('speaker-name');

    function updateStatus(message, speaker = '') {
        statusMessage.textContent = message;
        speakerName.textContent = speaker;
    }

    async function fetchFiles() {
        const response = await fetch('/api/media/allspeakers');
        if (response.ok) {
            files = await response.json();
            displayFiles();
            if (files.length > 0) {
                setDefaultFile(0);
                updatePreviousButtonState();
            }
        } else {
            alert('Failed to fetch speakers.');
        }
    }

    function setDefaultFile(index) {
        if (index < 0 || index >= files.length) return;
        currentFileIndex = index;
        const speaker = files[currentFileIndex];
        audioPlayer.src = `/api/media/stream/${encodeURIComponent(speaker.displayFileName)}`;
        console.log('Setting audio source:', audioPlayer.src); 
        const lastInitial = speaker.lastName ? speaker.lastName.charAt(0).toUpperCase() : '';
        const speakerName = `${speaker.firstName} ${lastInitial}`;
        updateStatus('Stopped', speakerName);
        updatePlayStatusBar();
        updatePreviousButtonState();
    }

    function displayFiles() {
        fileListDiv.innerHTML = '';
        files.forEach((speaker, index) => {
            const speakerRow = document.createElement('div');
            speakerRow.classList.add('speaker-row');
            speakerRow.addEventListener('mouseenter', () => speakerRow.classList.add('hover-effect'));
            speakerRow.addEventListener('mouseleave', () => speakerRow.classList.remove('hover-effect'));

            const speakerIdDiv = document.createElement('div');
            speakerIdDiv.textContent = speaker.speakerId;
            speakerIdDiv.classList.add('player-id-column');
            speakerRow.appendChild(speakerIdDiv);

            const displayNameDiv = document.createElement('div');
            const lastInitial = speaker.lastName ? speaker.lastName.charAt(0).toUpperCase() : '';
            const speakerName = `${speaker.firstName} ${lastInitial}`;
            displayNameDiv.textContent = speakerName;
            displayNameDiv.classList.add('player-speaker-column');
            speakerRow.appendChild(displayNameDiv);

            const descriptionDiv = document.createElement('div');
            descriptionDiv.textContent = speaker.description; // Directly use the description field
            descriptionDiv.classList.add('player-description-column');
            speakerRow.appendChild(descriptionDiv);

            speakerRow.addEventListener('click', () => playFile(index));
            speakerRow.classList.add(index % 2 === 0 ? 'even-line' : 'odd-line');
            fileListDiv.appendChild(speakerRow);
        });
    }

    function playFile(index) {
        if (index < 0 || index >= files.length) return;
        currentFileIndex = index;
        const speaker = files[currentFileIndex];
        audioPlayer.src = `/api/media/stream/${encodeURIComponent(speaker.displayFileName)}`;
        console.log('Playing audio:', audioPlayer.src); // Debugging
        audioPlayer.play();
        const lastInitial = speaker.lastName ? speaker.lastName.charAt(0).toUpperCase() : '';
        const speakerName = `${speaker.firstName} ${lastInitial}`;
        updateStatus('Playing...', speakerName);
        updatePlayStatusBar();
        updatePreviousButtonState();
    }

    document.getElementById('play').addEventListener('click', () => {
        if (audioPlayer.paused) {
            audioPlayer.play();
            const speaker = files[currentFileIndex];
            const lastInitial = speaker.lastName ? speaker.lastName.charAt(0).toUpperCase() : '';
            const speakerName = `${speaker.firstName} ${lastInitial}`;
            updateStatus('Playing...', speakerName);
            updatePlayStatusBar();
            updatePreviousButtonState();
        }
    });

    document.getElementById('pause').addEventListener('click', () => {
        if (!audioPlayer.paused) {
            audioPlayer.pause();
            const speaker = files[currentFileIndex];
            const lastInitial = speaker.lastName ? speaker.lastName.charAt(0).toUpperCase() : '';
            const speakerName = `${speaker.firstName} ${lastInitial}`;
            updateStatus('Paused...', speakerName);
            updatePlayStatusBar();
        }
    });
    document.getElementById('stop').addEventListener('click', () => {
        audioPlayer.pause();
        audioPlayer.currentTime = 0;
        const speaker = files[currentFileIndex];
        const lastInitial = speaker.lastName ? speaker.lastName.charAt(0).toUpperCase() : '';
        const speakerName = `${speaker.firstName} ${lastInitial}`;
        updateStatus('Stopped', speakerName);
        stopPulsing(); // Ensure pulsing stops
        updatePlayStatusBar();
    });

    document.getElementById('previous').addEventListener('click', () => {
        if (currentFileIndex > 0) {
            playFile(currentFileIndex - 1);
            updatePreviousButtonState();
        }
    });

    document.getElementById('next').addEventListener('click', () => {
        if (currentFileIndex < files.length - 1) {
            playFile(currentFileIndex + 1);
            updatePreviousButtonState();
        }
    });

    audioPlayer.addEventListener('play', () => {
        const speaker = files[currentFileIndex];
        const lastInitial = speaker.lastName ? speaker.lastName.charAt(0).toUpperCase() : '';
        const speakerName = `${speaker.firstName} ${lastInitial}`;
        updateStatus('Playing...', speakerName);
        updatePlayStatusBar();
        startPulsing();
    });

    audioPlayer.addEventListener('pause', () => {
        if (audioPlayer.currentTime === 0) {
            stopPulsing();
        } else {
            const speaker = files[currentFileIndex];
            const lastInitial = speaker.lastName ? speaker.lastName.charAt(0).toUpperCase() : '';
            const speakerName = `${speaker.firstName} ${lastInitial}`;
            updateStatus('Paused...', speakerName);
            updatePlayStatusBar();
            startPulsing();
        }
    });

    audioPlayer.addEventListener('ended', () => {
        const speaker = files[currentFileIndex];
        const lastInitial = speaker.lastName ? speaker.lastName.charAt(0).toUpperCase() : '';
        const speakerName = `${speaker.firstName} ${lastInitial}`;
        updateStatus('Stopped', speakerName);
        updatePlayStatusBar();
        stopPulsing();
    });

    fetchFiles();

    const searchInput = document.getElementById('searchInput');
    searchInput.addEventListener('input', filterFiles);

    function filterFiles() {
        const searchTerm = searchInput.value.toLowerCase();
        const fileItems = document.querySelectorAll('.speaker-row');
        fileItems.forEach((fileItem) => {
            const speakerIdColumn = fileItem.querySelector('.player-id-column');
            const displayNameColumn = fileItem.querySelector('.player-speaker-column');
            //const descriptionColumn = fileItem.querySelector('.player-description-column');
            //const rowText = `${speakerIdColumn.textContent} ${displayNameColumn.textContent} ${descriptionColumn.textContent}`.toLowerCase() ;
            const rowText = `${speakerIdColumn.textContent} ${displayNameColumn.textContent}`.toLowerCase() ;
            fileItem.style.display = rowText.includes(searchTerm) ? 'flex' : 'none';
        });
    }

    const playStatusBar = document.getElementById('play-status-bar');
    const playStatusHandle = document.getElementById('play-status-handle');
    const currentTimeDisplay = document.getElementById('current-time');
    const totalDurationDisplay = document.getElementById('total-duration');

    let isDragging = false;

    playStatusBar.addEventListener('mousedown', (e) => {
        isDragging = true;
        updateAudioTime(e);
    });

    document.addEventListener('mousemove', (e) => {
        if (isDragging) {
            updateAudioTime(e);
        }
    });

    document.addEventListener('mouseup', () => {
        isDragging = false;
    });

    playStatusBar.addEventListener('click', (e) => {
        updateAudioTime(e);
    });

    function updateAudioTime(e) {
        const rect = playStatusBar.getBoundingClientRect();
        const offsetX = e.clientX - rect.left;
        const percentage = offsetX / rect.width;
        const newTime = audioPlayer.duration * percentage;
        audioPlayer.currentTime = newTime;
        updatePlayStatusBar();
    }

    function updatePlayStatusBar() {
        const progressBar = document.getElementById('play-status-progress');
        const duration = audioPlayer.duration;
        const currentTime = audioPlayer.currentTime;
        console.log('Updating status bar', { duration, currentTime }); // Debugging
        if (duration > 0) {
            const progressPercentage = (currentTime / duration) * 100;
            progressBar.style.width = `${progressPercentage}%`;
            playStatusHandle.style.left = `${progressPercentage}%`;
            currentTimeDisplay.textContent = formatTime(currentTime);
            totalDurationDisplay.textContent = formatTime(duration);
        } else {
            progressBar.style.width = '0%';
            playStatusHandle.style.left = '0%';
            currentTimeDisplay.textContent = '00:00';
            totalDurationDisplay.textContent = '00:00';
        }
    }

    function formatTime(time) {
        const minutes = Math.floor(time / 60);
        const seconds = Math.floor(time % 60);
        return `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
    }

    audioPlayer.addEventListener('timeupdate', updatePlayStatusBar);
    audioPlayer.addEventListener('loadedmetadata', () => {
        console.log('Audio metadata loaded:', audioPlayer.duration); // Debugging
        totalDurationDisplay.textContent = formatTime(audioPlayer.duration);
    });

    let pulseInterval; // Variable to store the interval ID for pulsing

    function startPulsing() {
        const statusMessage = document.getElementById('status-message');
        let opacity = 1; // Start with full opacity

        // Clear any existing interval to avoid multiple pulses
        clearInterval(pulseInterval);

        // Set up the interval to animate the opacity
        pulseInterval = setInterval(() => {
            opacity = opacity === 1 ? 0.2 : 1; // Toggle opacity between 1 and 0.2
            statusMessage.style.opacity = opacity; // Update the opacity
        }, 1000); // Toggle every 500ms (0.5 seconds)
    }

    function stopPulsing() {
        const statusMessage = document.getElementById('status-message');
        clearInterval(pulseInterval); // Stop the interval
        statusMessage.style.opacity = 1; // Reset opacity to 1
    }

    function updatePreviousButtonState() {
        const previousButton = document.getElementById('previous');
        previousButton.disabled = currentFileIndex === 0;
    }
});