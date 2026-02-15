// ascier - klient webowy
// renderowanie ascii na canvas, komunikacja z serwerem przez api i signalr

(function () {
    'use strict';

    // elementy dom
    const fileInput = document.getElementById('fileInput');
    const effectSelect = document.getElementById('effectSelect');
    const stepRange = document.getElementById('stepRange');
    const stepValue = document.getElementById('stepValue');
    const maxColsRange = document.getElementById('maxColsRange');
    const maxColsValue = document.getElementById('maxColsValue');
    const thresholdRange = document.getElementById('thresholdRange');
    const thresholdValue = document.getElementById('thresholdValue');
    const colorMode = document.getElementById('colorMode');
    const invertMode = document.getElementById('invertMode');
    const convertBtn = document.getElementById('convertBtn');
    const downloadBtn = document.getElementById('downloadBtn');
    const canvas = document.getElementById('asciiCanvas');
    const ctx = canvas.getContext('2d');
    const placeholder = document.getElementById('placeholder');
    const fileInfo = document.getElementById('fileInfo');
    const statsText = document.getElementById('statsText');
    const videoControls = document.getElementById('videoControls');
    const frameInfo = document.getElementById('frameInfo');
    const frameSlider = document.getElementById('frameSlider');
    const videoMeta = document.getElementById('videoMeta');
    const prevFrameBtn = document.getElementById('prevFrame');
    const nextFrameBtn = document.getElementById('nextFrame');
    const playBtn = document.getElementById('playBtn');

    // stan aplikacji
    let currentFile = null;
    let isVideo = false;
    let videoSessionId = null;
    let currentFrame = 0;
    let totalFrames = 0;
    let videoFps = 30;
    let isPlaying = false;
    let playTimer = null;
    let lastAsciiText = '';
    let connection = null;

    // czcionka do renderowania ascii na canvasie
    const FONT_SIZE = 10;
    const FONT = `${FONT_SIZE}px monospace`;
    let charWidth = 0;
    let charHeight = 0;

    // pomiar szerokości znaku
    function measureChar() {
        ctx.font = FONT;
        const m = ctx.measureText('M');
        charWidth = m.width;
        charHeight = FONT_SIZE * 1.15;
    }

    // inicjalizacja signalr
    function initSignalR() {
        connection = new signalR.HubConnectionBuilder()
            .withUrl('/hub/conversion')
            .withAutomaticReconnect()
            .build();

        connection.on('ReceiveFrame', (data) => {
            renderFrame(data);
            updateStats(data);
        });

        connection.on('Error', (msg) => {
            console.error('serwer:', msg);
        });

        connection.start().catch(err => {
            console.warn('signalr niedostępny, używam rest api:', err.message);
        });
    }

    // pobranie ustawień z kontrolek
    function getSettings() {
        return {
            effect: effectSelect.value,
            step: parseInt(stepRange.value),
            colorMode: colorMode.checked,
            threshold: parseInt(thresholdRange.value),
            invert: invertMode.checked,
            maxColumns: parseInt(maxColsRange.value)
        };
    }

    // budowanie form data do wysyłki
    function buildFormData(file, settings, extraFields) {
        const fd = new FormData();
        if (file) fd.append('file', file);
        fd.append('effect', settings.effect);
        fd.append('step', settings.step);
        fd.append('colorMode', settings.colorMode);
        fd.append('threshold', settings.threshold);
        fd.append('invert', settings.invert);
        fd.append('maxColumns', settings.maxColumns);
        if (extraFields) {
            for (const [k, v] of Object.entries(extraFields)) {
                fd.append(k, v);
            }
        }
        return fd;
    }

    // konwersja obrazu
    async function convertImage() {
        if (!currentFile) return;

        convertBtn.disabled = true;
        convertBtn.textContent = 'przetwarzam...';
        const startTime = performance.now();

        try {
            const settings = getSettings();
            const fd = buildFormData(currentFile, settings);

            const resp = await fetch('/api/convert/image', { method: 'POST', body: fd });
            if (!resp.ok) {
                const err = await resp.text();
                alert('błąd: ' + err);
                return;
            }

            const data = await resp.json();
            const elapsed = performance.now() - startTime;

            renderFrame(data);
            updateStats(data, elapsed);
            lastAsciiText = data.text;
            downloadBtn.disabled = false;
        } catch (e) {
            console.error('błąd konwersji:', e);
            alert('błąd konwersji: ' + e.message);
        } finally {
            convertBtn.disabled = false;
            convertBtn.textContent = 'konwertuj';
        }
    }

    // upload wideo i inicjalizacja sesji
    async function uploadVideo() {
        if (!currentFile) return;

        convertBtn.disabled = true;
        convertBtn.textContent = 'uploaduję...';

        try {
            const settings = getSettings();
            const fd = buildFormData(currentFile, settings);

            const resp = await fetch('/api/convert/video', { method: 'POST', body: fd });
            if (!resp.ok) {
                const err = await resp.text();
                alert('błąd: ' + err);
                return;
            }

            const data = await resp.json();
            videoSessionId = data.sessionId;
            totalFrames = data.totalFrames;
            videoFps = data.fps || 30;
            currentFrame = 0;

            frameSlider.max = totalFrames - 1;
            frameSlider.value = 0;
            videoMeta.textContent = `${data.width}x${data.height} | ${videoFps.toFixed(1)}fps | ${data.duration.toFixed(1)}s`;
            videoControls.style.display = 'block';

            // załaduj pierwszą klatkę
            await loadVideoFrame(0);
        } catch (e) {
            console.error('błąd uploadu wideo:', e);
            alert('błąd uploadu: ' + e.message);
        } finally {
            convertBtn.disabled = false;
            convertBtn.textContent = 'konwertuj';
        }
    }

    // załaduj konkretną klatkę wideo
    async function loadVideoFrame(frameNumber) {
        if (!videoSessionId) return;

        const startTime = performance.now();
        const settings = getSettings();
        const fd = buildFormData(null, settings, {
            sessionId: videoSessionId,
            frameNumber: frameNumber
        });

        try {
            const resp = await fetch('/api/convert/frame', { method: 'POST', body: fd });
            if (!resp.ok) return;

            const data = await resp.json();
            const elapsed = performance.now() - startTime;

            currentFrame = data.frameNumber;
            frameSlider.value = currentFrame;
            frameInfo.textContent = `${currentFrame + 1}/${data.totalFrames}`;

            renderFrame(data);
            updateStats(data, elapsed);
            lastAsciiText = data.text;
            downloadBtn.disabled = false;
        } catch (e) {
            console.error('błąd ładowania klatki:', e);
        }
    }

    // renderowanie ramki ascii na canvasie
    function renderFrame(data) {
        if (!data || !data.text) return;

        placeholder.style.display = 'none';
        canvas.style.display = 'block';

        measureChar();

        const cols = data.columns;
        const rows = data.rows;

        // rozmiar canvasa dopasowany do treści
        canvas.width = Math.ceil(cols * charWidth) + 2;
        canvas.height = Math.ceil(rows * charHeight) + 2;

        // czyszczenie - czarne tło
        ctx.fillStyle = '#0a0a0f';
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        ctx.font = FONT;
        ctx.textBaseline = 'top';

        // dekodowanie kolorów jeśli dostępne
        let colors = null;
        if (data.colors) {
            const binary = atob(data.colors);
            colors = new Uint8Array(binary.length);
            for (let i = 0; i < binary.length; i++) {
                colors[i] = binary.charCodeAt(i);
            }
        }

        // renderowanie znak po znaku
        const lines = data.text.split('\n');
        let charIdx = 0;

        for (let y = 0; y < lines.length && y < rows; y++) {
            const line = lines[y];
            for (let x = 0; x < line.length && x < cols; x++) {
                const ch = line[x];
                if (ch === ' ') {
                    charIdx++;
                    continue;
                }

                if (colors) {
                    const ci = charIdx * 3;
                    ctx.fillStyle = `rgb(${colors[ci]},${colors[ci + 1]},${colors[ci + 2]})`;
                } else {
                    // domyślny kolor zielony (terminal-style)
                    ctx.fillStyle = '#00ff88';
                }

                ctx.fillText(ch, x * charWidth, y * charHeight);
                charIdx++;
            }
        }
    }

    // aktualizacja statystyk
    function updateStats(data, elapsedMs) {
        const cols = data.columns || 0;
        const rows = data.rows || 0;
        const chars = cols * rows;
        const hasColors = !!data.colors;
        const effect = effectSelect.value;

        let text = `${cols}×${rows} = ${chars.toLocaleString()} znaków\n`;
        text += `efekt: ${effect}`;
        if (hasColors) text += ' [kolor]';
        if (elapsedMs) text += `\nczas: ${elapsedMs.toFixed(0)}ms`;
        if (data.totalFrames > 1) text += `\nklatka: ${(data.frameNumber || 0) + 1}/${data.totalFrames}`;

        statsText.textContent = text;
    }

    // odtwarzanie wideo
    function startPlayback() {
        if (isPlaying || !videoSessionId) return;
        isPlaying = true;
        playBtn.textContent = '⏸ pause';

        const frameDelay = Math.max(50, 1000 / Math.min(videoFps, 10)); // max 10fps dla preview
        playTimer = setInterval(async () => {
            if (currentFrame >= totalFrames - 1) {
                stopPlayback();
                return;
            }
            currentFrame++;
            await loadVideoFrame(currentFrame);
        }, frameDelay);
    }

    function stopPlayback() {
        isPlaying = false;
        playBtn.textContent = '▶ play';
        if (playTimer) {
            clearInterval(playTimer);
            playTimer = null;
        }
    }

    // eksport ascii do pliku tekstowego
    function downloadAscii() {
        if (!lastAsciiText) return;

        const blob = new Blob([lastAsciiText], { type: 'text/plain;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'ascii_art.txt';
        a.click();
        URL.revokeObjectURL(url);
    }

    // event handlers
    fileInput.addEventListener('change', (e) => {
        currentFile = e.target.files[0];
        if (!currentFile) {
            convertBtn.disabled = true;
            return;
        }

        // detekcja typu pliku
        const ext = currentFile.name.split('.').pop().toLowerCase();
        const videoExts = new Set(['mp4', 'avi', 'mkv', 'webm', 'mov', 'flv', 'wmv']);
        isVideo = videoExts.has(ext);

        const sizeMb = (currentFile.size / (1024 * 1024)).toFixed(2);
        fileInfo.textContent = `${currentFile.name} (${sizeMb} MB) ${isVideo ? '🎬 wideo' : '🖼️ obraz'}`;

        // reset stanu wideo
        videoSessionId = null;
        videoControls.style.display = 'none';
        stopPlayback();

        convertBtn.disabled = false;
    });

    convertBtn.addEventListener('click', () => {
        if (isVideo) {
            uploadVideo();
        } else {
            convertImage();
        }
    });

    downloadBtn.addEventListener('click', downloadAscii);

    // suwaki - aktualizacja wartości w label
    stepRange.addEventListener('input', () => { stepValue.textContent = stepRange.value; });
    maxColsRange.addEventListener('input', () => { maxColsValue.textContent = maxColsRange.value; });
    thresholdRange.addEventListener('input', () => { thresholdValue.textContent = thresholdRange.value; });

    // auto-reconvert przy zmianie ustawień (dla obrazów)
    const autoReconvert = () => {
        if (!isVideo && currentFile && lastAsciiText) {
            convertImage();
        }
    };

    effectSelect.addEventListener('change', autoReconvert);

    // kontrola wideo
    prevFrameBtn.addEventListener('click', () => {
        stopPlayback();
        if (currentFrame > 0) {
            currentFrame--;
            loadVideoFrame(currentFrame);
        }
    });

    nextFrameBtn.addEventListener('click', () => {
        stopPlayback();
        if (currentFrame < totalFrames - 1) {
            currentFrame++;
            loadVideoFrame(currentFrame);
        }
    });

    playBtn.addEventListener('click', () => {
        if (isPlaying) stopPlayback();
        else startPlayback();
    });

    frameSlider.addEventListener('input', () => {
        stopPlayback();
        currentFrame = parseInt(frameSlider.value);
        loadVideoFrame(currentFrame);
    });

    // re-konwersja klatki wideo przy zmianie ustawień
    effectSelect.addEventListener('change', () => {
        if (isVideo && videoSessionId) loadVideoFrame(currentFrame);
    });

    // inicjalizacja
    measureChar();
    initSignalR();
})();
