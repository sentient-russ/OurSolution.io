const audioPlayer = document.getElementById('audioPlayer');
const fileListDiv = document.getElementById('fileList');
let files = [];
let currentFileIndex = 0;
let statusMessage = document.getElementById('status-message');

// Fetch the list of MP3 files from the server
async function fetchFiles() {
    const response = await fetch('/api/media/files');
    if (response.ok) {
        files = await response.json();
        displayFiles();

        // Set the first file as the default source
        if (files.length > 0) {
            playFile(0); // Automatically set the first file as the source
        }
    } else {
        alert('Failed to fetch files.');
    }
}

// Display the list of files
function displayFiles() {
    fileListDiv.innerHTML = '';
    files.forEach((file, index) => {
        const fileDiv = document.createElement('div');
        fileDiv.textContent = file;
        fileDiv.addEventListener('click', () => playFile(index));
        fileListDiv.appendChild(fileDiv);
    });
}

// Play a specific file
function playFile(index) {
    if (index < 0 || index >= files.length) return;
    currentFileIndex = index;
    const fileName = files[currentFileIndex];
    audioPlayer.src = `/api/media/stream/${encodeURIComponent(fileName)}`;
    audioPlayer.play(); // Automatically start playing the file
}

// Play button
document.getElementById('play').addEventListener('click', () => {
    audioPlayer.play();
});

// Pause button
document.getElementById('pause').addEventListener('click', () => {
    audioPlayer.pause();
});

// Stop button
document.getElementById('stop').addEventListener('click', () => {
    audioPlayer.pause();
    audioPlayer.currentTime = 0;
});

// Last button
document.getElementById('previous').addEventListener('click', () => {
    if (currentFileIndex > 0) {
        playFile(currentFileIndex - 1);
    }
});

// Next button
document.getElementById('next').addEventListener('click', () => {
    if (currentFileIndex < files.length - 1) {
        playFile(currentFileIndex + 1);
    }
});




// Initialize the file list
fetchFiles();