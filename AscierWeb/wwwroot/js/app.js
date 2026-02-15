// ascier web - klient
// tekst w <pre>, progress bars, logi w czasie rzeczywistym

(function () {
    'use strict';

    // dom
    const fileInput = document.getElementById('fileInput');
    const effectSelect = document.getElementById('effectSelect');
    const stepRange = document.getElementById('stepRange');
    const stepValue = document.getElementById('stepValue');
    const maxColsRange = document.getElementById('maxColsRange');
    const maxColsValue = document.getElementById('maxColsValue');
    const thresholdRange = document.getElementById('thresholdRange');
    const thresholdValue = document.getElementById('thresholdValue');
    const fontSizeRange = document.getElementById('fontSizeRange');
    const fontSizeValue = document.getElementById('fontSizeValue');
    const colorMode = document.getElementById('colorMode');
    const invertMode = document.getElementById('invertMode');
    const convertBtn = document.getElementById('convertBtn');
    const downloadBtn = document.getElementById('downloadBtn');
    const asciiOutput = document.getElementById('asciiOutput');
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
    const progressGroup = document.getElementById('progressGroup');
    const progressLabel = document.getElementById('progressLabel');
    const progressFill = document.getElementById('progressFill');
    const logToggle = document.getElementById('logToggle');
    const logBody = document.getElementById('logBody');
    const logOutput = document.getElementById('logOutput');
    const logBadge = document.getElementById('logBadge');
    const logArrow = document.getElementById('logArrow');
    const clearLogs = document.getElementById('clearLogs');

    // state
    let currentFile = null;
    let isVideo = false;
    let videoSessionId = null;
    let currentFrame = 0;
    let totalFrames = 0;
    let videoFps = 30;
    let isPlaying = false;
    let lastAsciiText = '';
    let connection = null;
    let logCount = 0;
    let logOpen = false;
    let converting = false;

    // -- progress --

    function showProgress(label, pct) {
        progressGroup.style.display = 'block';
        progressLabel.textContent = label;
        if (pct >= 0) {
            progressFill.style.width = pct + '%';
            progressFill.classList.remove('indeterminate');
        } else {
            progressFill.style.width = '100%';
            progressFill.classList.add('indeterminate');
        }
    }

    function hideProgress() {
        progressGroup.style.display = 'none';
        progressFill.style.width = '0%';
        progressFill.classList.remove('indeterminate');
    }

    // -- signalr --

    function initSignalR() {
        connection = new signalR.HubConnectionBuilder()
            .withUrl('/hub/conversion')
            .withAutomaticReconnect()
            .build();

        connection.on('NewLog', function (entry) {
            appendLog(entry);
        });

        connection.on('ReceiveFrame', function (data) {
            renderText(data);
            updateStats(data);
        });

        connection.on('Error', function (msg) {
            appendLog({ timestamp: timeNow(), level: 'error', message: msg });
        });

        connection.start().then(function () {
            connection.invoke('SubscribeLogs').catch(function () {});
            appendLog({ timestamp: timeNow(), level: 'info', message: 'połączono z serwerem' });
        }).catch(function (err) {
            appendLog({ timestamp: timeNow(), level: 'warn', message: 'signalr: ' + err.message });
        });
    }

    function timeNow() {
        var d = new Date();
        var h = String(d.getHours()).padStart(2, '0');
        var m = String(d.getMinutes()).padStart(2, '0');
        var s = String(d.getSeconds()).padStart(2, '0');
        var ms = String(d.getMilliseconds()).padStart(3, '0');
        return h + ':' + m + ':' + s + '.' + ms;
    }

    // -- logs --

    function appendLog(entry) {
        var cls = 'log-line-' + entry.level;
        var line = document.createElement('span');
        line.className = cls;
        line.textContent = entry.timestamp + ' [' + entry.level + '] ' + entry.message + '\n';
        logOutput.appendChild(line);
        logOutput.scrollTop = logOutput.scrollHeight;
        logCount++;
        logBadge.textContent = logCount;
        if (entry.level === 'error') {
            logBadge.classList.add('error');
        }
    }

    // -- settings --

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

    function buildFormData(file, settings, extra) {
        var fd = new FormData();
        if (file) fd.append('file', file);
        fd.append('effect', settings.effect);
        fd.append('step', settings.step);
        fd.append('colorMode', settings.colorMode);
        fd.append('threshold', settings.threshold);
        fd.append('invert', settings.invert);
        fd.append('maxColumns', settings.maxColumns);
        if (extra) {
            Object.keys(extra).forEach(function (k) { fd.append(k, extra[k]); });
        }
        return fd;
    }

    // -- upload with progress --

    function uploadWithProgress(url, formData, onProgress) {
        return new Promise(function (resolve, reject) {
            var xhr = new XMLHttpRequest();
            xhr.open('POST', url);

            xhr.upload.addEventListener('progress', function (e) {
                if (e.lengthComputable) onProgress(e.loaded / e.total);
            });

            xhr.addEventListener('load', function () {
                if (xhr.status >= 200 && xhr.status < 300) {
                    try {
                        resolve(JSON.parse(xhr.responseText));
                    } catch (e) {
                        reject(new Error('nieprawidłowa odpowiedź serwera'));
                    }
                } else {
                    reject(new Error(xhr.responseText || 'HTTP ' + xhr.status));
                }
            });

            xhr.addEventListener('error', function () {
                reject(new Error('błąd sieci'));
            });

            xhr.send(formData);
        });
    }

    // -- html escape --

    function esc(ch) {
        if (ch === '&') return '&amp;';
        if (ch === '<') return '&lt;';
        if (ch === '>') return '&gt;';
        return ch;
    }

    // -- render text output --

    function renderText(data) {
        if (!data || !data.text) return;

        placeholder.style.display = 'none';
        asciiOutput.style.display = 'block';

        if (data.colors) {
            // tryb kolorowy - span per znak
            var binary = atob(data.colors);
            var colors = new Uint8Array(binary.length);
            for (var i = 0; i < binary.length; i++) {
                colors[i] = binary.charCodeAt(i);
            }

            var lines = data.text.split('\n');
            var parts = [];
            var ci = 0;

            for (var y = 0; y < lines.length; y++) {
                var line = lines[y];
                if (line.length === 0) continue;

                for (var x = 0; x < line.length; x++) {
                    var idx = ci * 3;
                    var r = colors[idx] || 0;
                    var g = colors[idx + 1] || 0;
                    var b = colors[idx + 2] || 0;
                    parts.push('<span style="color:rgb(' + r + ',' + g + ',' + b + ')">' + esc(line[x]) + '</span>');
                    ci++;
                }
                parts.push('\n');
            }

            asciiOutput.innerHTML = parts.join('');
        } else {
            // zwykły tekst
            asciiOutput.textContent = data.text;
        }

        lastAsciiText = data.text;
        downloadBtn.disabled = false;
    }

    function updateStats(data, ms) {
        var cols = data.columns || 0;
        var rows = data.rows || 0;
        var t = cols + '×' + rows + ' = ' + (cols * rows).toLocaleString() + ' znaków';
        t += '\nefekt: ' + effectSelect.value;
        if (data.colors) t += ' [kolor]';
        if (ms) t += '\nczas: ' + ms.toFixed(0) + 'ms';
        if (data.totalFrames > 1) t += '\nklatka: ' + ((data.frameNumber || 0) + 1) + '/' + data.totalFrames;
        statsText.textContent = t;
    }

    // -- image conversion --

    function convertImage() {
        if (!currentFile || converting) return;
        converting = true;
        convertBtn.disabled = true;
        convertBtn.textContent = '...';

        var settings = getSettings();
        var fd = buildFormData(currentFile, settings);
        var start = performance.now();

        showProgress('upload...', 0);

        uploadWithProgress('/api/convert/image', fd, function (pct) {
            showProgress('upload ' + Math.round(pct * 100) + '%', pct * 100);
            if (pct >= 1) showProgress('przetwarzam...', -1);
        }).then(function (data) {
            var elapsed = performance.now() - start;
            hideProgress();
            renderText(data);
            updateStats(data, elapsed);
        }).catch(function (e) {
            hideProgress();
            appendLog({ timestamp: timeNow(), level: 'error', message: 'konwersja: ' + e.message });
        }).finally(function () {
            converting = false;
            convertBtn.disabled = false;
            convertBtn.textContent = 'konwertuj';
        });
    }

    // -- video upload --

    function uploadVideo() {
        if (!currentFile || converting) return;
        converting = true;
        convertBtn.disabled = true;
        convertBtn.textContent = '...';

        var settings = getSettings();
        var fd = buildFormData(currentFile, settings);

        showProgress('upload wideo...', 0);

        uploadWithProgress('/api/convert/video', fd, function (pct) {
            showProgress('upload ' + Math.round(pct * 100) + '%', pct * 100);
            if (pct >= 1) showProgress('analizuję wideo...', -1);
        }).then(function (data) {
            hideProgress();
            videoSessionId = data.sessionId;
            totalFrames = data.totalFrames;
            videoFps = data.fps || 30;
            currentFrame = 0;

            frameSlider.max = totalFrames - 1;
            frameSlider.value = 0;
            videoMeta.textContent = data.width + 'x' + data.height + ' | ' +
                videoFps.toFixed(1) + 'fps | ' +
                data.duration.toFixed(1) + 's | ' +
                totalFrames + ' klatek';
            videoControls.style.display = 'block';

            converting = false;
            convertBtn.disabled = false;
            convertBtn.textContent = 'konwertuj';

            // załaduj pierwszą klatkę
            return loadVideoFrame(0);
        }).catch(function (e) {
            hideProgress();
            appendLog({ timestamp: timeNow(), level: 'error', message: 'upload wideo: ' + e.message });
            converting = false;
            convertBtn.disabled = false;
            convertBtn.textContent = 'konwertuj';
        });
    }

    // -- video frame loading --

    function loadVideoFrame(frameNumber) {
        if (!videoSessionId) return Promise.resolve();

        showProgress('klatka ' + (frameNumber + 1) + '/' + totalFrames, -1);

        var start = performance.now();
        var settings = getSettings();
        var fd = buildFormData(null, settings, {
            sessionId: videoSessionId,
            frameNumber: frameNumber
        });

        return fetch('/api/convert/frame', { method: 'POST', body: fd })
            .then(function (resp) {
                if (!resp.ok) {
                    return resp.text().then(function (t) { throw new Error(t); });
                }
                return resp.json();
            })
            .then(function (data) {
                var elapsed = performance.now() - start;
                currentFrame = data.frameNumber;
                frameSlider.value = currentFrame;
                frameInfo.textContent = (currentFrame + 1) + '/' + data.totalFrames;
                renderText(data);
                updateStats(data, elapsed);
                hideProgress();
            })
            .catch(function (e) {
                hideProgress();
                appendLog({ timestamp: timeNow(), level: 'error', message: 'klatka ' + frameNumber + ': ' + e.message });
            });
    }

    // -- video playback --

    function sleep(ms) {
        return new Promise(function (r) { setTimeout(r, ms); });
    }

    function startPlayback() {
        if (isPlaying || !videoSessionId) return;
        isPlaying = true;
        playBtn.textContent = '⏸';

        var frameDelay = Math.max(100, 1000 / Math.min(videoFps, 8));

        (function loop() {
            if (!isPlaying || currentFrame >= totalFrames - 1) {
                stopPlayback();
                return;
            }
            currentFrame++;
            var start = performance.now();
            loadVideoFrame(currentFrame).then(function () {
                if (!isPlaying) return;
                var elapsed = performance.now() - start;
                var wait = Math.max(0, frameDelay - elapsed);
                return sleep(wait);
            }).then(function () {
                if (isPlaying) loop();
            });
        })();
    }

    function stopPlayback() {
        isPlaying = false;
        playBtn.textContent = '▶ play';
    }

    // -- download .txt --

    function downloadTxt() {
        if (!lastAsciiText) return;
        var blob = new Blob([lastAsciiText], { type: 'text/plain;charset=utf-8' });
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = 'ascii_art.txt';
        a.click();
        URL.revokeObjectURL(url);
    }

    // -- event listeners --

    fileInput.addEventListener('change', function (e) {
        currentFile = e.target.files[0];
        if (!currentFile) {
            convertBtn.disabled = true;
            return;
        }

        var ext = currentFile.name.split('.').pop().toLowerCase();
        isVideo = ['mp4', 'avi', 'mkv', 'webm', 'mov', 'flv', 'wmv'].indexOf(ext) !== -1;
        var mb = (currentFile.size / 1048576).toFixed(2);
        fileInfo.textContent = currentFile.name + ' (' + mb + ' MB)' + (isVideo ? ' 🎬' : ' 🖼️');

        videoSessionId = null;
        videoControls.style.display = 'none';
        stopPlayback();
        convertBtn.disabled = false;
    });

    convertBtn.addEventListener('click', function () {
        if (isVideo) uploadVideo();
        else convertImage();
    });

    downloadBtn.addEventListener('click', downloadTxt);

    stepRange.addEventListener('input', function () {
        stepValue.textContent = stepRange.value;
    });
    maxColsRange.addEventListener('input', function () {
        maxColsValue.textContent = maxColsRange.value;
    });
    thresholdRange.addEventListener('input', function () {
        thresholdValue.textContent = thresholdRange.value;
    });
    fontSizeRange.addEventListener('input', function () {
        fontSizeValue.textContent = fontSizeRange.value;
        asciiOutput.style.fontSize = fontSizeRange.value + 'px';
        asciiOutput.style.lineHeight = Math.round(parseInt(fontSizeRange.value) * 1.1) + 'px';
    });

    prevFrameBtn.addEventListener('click', function () {
        stopPlayback();
        if (currentFrame > 0) {
            currentFrame--;
            loadVideoFrame(currentFrame);
        }
    });

    nextFrameBtn.addEventListener('click', function () {
        stopPlayback();
        if (currentFrame < totalFrames - 1) {
            currentFrame++;
            loadVideoFrame(currentFrame);
        }
    });

    playBtn.addEventListener('click', function () {
        if (isPlaying) stopPlayback();
        else startPlayback();
    });

    frameSlider.addEventListener('input', function () {
        stopPlayback();
        currentFrame = parseInt(frameSlider.value);
        loadVideoFrame(currentFrame);
    });

    // log panel toggle
    logToggle.addEventListener('click', function () {
        logOpen = !logOpen;
        logBody.style.display = logOpen ? 'block' : 'none';
        logArrow.textContent = logOpen ? '▼' : '▲';
        if (logOpen) logBadge.classList.remove('error');
    });

    clearLogs.addEventListener('click', function () {
        logOutput.textContent = '';
        logCount = 0;
        logBadge.textContent = '0';
        logBadge.classList.remove('error');
    });

    // init
    initSignalR();
})();
