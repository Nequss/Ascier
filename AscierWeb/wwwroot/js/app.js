// ascier web - video player z preload, batch i buforowaniem klatek

(function () {
    'use strict';

    // dom refs
    var $ = function (id) { return document.getElementById(id); };
    var fileInput = $('fileInput');
    var effectSelect = $('effectSelect');
    var stepRange = $('stepRange'), stepValue = $('stepValue');
    var maxColsRange = $('maxColsRange'), maxColsValue = $('maxColsValue');
    var thresholdRange = $('thresholdRange'), thresholdValue = $('thresholdValue');
    var fontSizeRange = $('fontSizeRange'), fontSizeValue = $('fontSizeValue');
    var colorMode = $('colorMode'), invertMode = $('invertMode');
    var convertBtn = $('convertBtn'), downloadBtn = $('downloadBtn');
    var asciiOutput = $('asciiOutput'), placeholder = $('placeholder');
    var fileInfo = $('fileInfo'), statsText = $('statsText');
    var videoControls = $('videoControls');
    var frameInfo = $('frameInfo'), frameSlider = $('frameSlider');
    var videoMeta = $('videoMeta'), bufferInfoEl = $('bufferInfo');
    var prevFrameBtn = $('prevFrame'), nextFrameBtn = $('nextFrame');
    var playBtn = $('playBtn'), stopBtn = $('stopBtn');
    var speedRange = $('speedRange'), speedValue = $('speedValue');
    var progressGroup = $('progressGroup');
    var progressLabel = $('progressLabel'), progressFill = $('progressFill');
    var logToggle = $('logToggle'), logBody = $('logBody');
    var logOutput = $('logOutput'), logBadge = $('logBadge');
    var logArrow = $('logArrow'), clearLogs = $('clearLogs');

    // state
    var currentFile = null;
    var isVideo = false;
    var videoSessionId = null;
    var currentFrame = 0;
    var totalFrames = 0;
    var videoFps = 30;
    var isPlaying = false;
    var playRafId = null;
    var lastAsciiText = '';
    var connection = null;
    var logCount = 0;
    var logOpen = false;
    var converting = false;
    var preloaded = false;
    var playSpeed = 1.0;

    // bufor klatek - tablica ascii text + colors dla każdej klatki
    var frameBuffer = [];
    var bufferBatchSize = 30;
    var bufferingFrom = -1;
    var bufferingPromise = null;

    // -- progress --

    function showProgress(label, pct) {
        progressGroup.style.display = 'block';
        progressLabel.textContent = label;
        if (pct >= 0) {
            progressFill.style.width = pct + '%';
            progressFill.classList.remove('indeterminate');
        } else {
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

        connection.on('NewLog', function (e) { appendLog(e); });
        connection.on('ReceiveFrame', function (d) { renderText(d); updateStats(d); });
        connection.on('Error', function (m) { appendLog({ timestamp: timeNow(), level: 'error', message: m }); });

        connection.start().then(function () {
            connection.invoke('SubscribeLogs').catch(function () {});
        }).catch(function (err) {
            appendLog({ timestamp: timeNow(), level: 'warn', message: 'signalr: ' + err.message });
        });
    }

    function timeNow() {
        var d = new Date();
        return [d.getHours(), d.getMinutes(), d.getSeconds()].map(function (n) {
            return String(n).padStart(2, '0');
        }).join(':') + '.' + String(d.getMilliseconds()).padStart(3, '0');
    }

    // -- logs --

    function appendLog(entry) {
        var span = document.createElement('span');
        span.className = 'log-line-' + entry.level;
        span.textContent = entry.timestamp + ' [' + entry.level + '] ' + entry.message + '\n';
        logOutput.appendChild(span);
        logOutput.scrollTop = logOutput.scrollHeight;
        logCount++;
        logBadge.textContent = logCount;
        if (entry.level === 'error') logBadge.classList.add('error');
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

    // -- upload with XHR progress --

    function uploadXHR(url, fd, onProgress) {
        return new Promise(function (resolve, reject) {
            var xhr = new XMLHttpRequest();
            xhr.open('POST', url);
            xhr.upload.addEventListener('progress', function (e) {
                if (e.lengthComputable) onProgress(e.loaded / e.total);
            });
            xhr.addEventListener('load', function () {
                if (xhr.status >= 200 && xhr.status < 300) {
                    try { resolve(JSON.parse(xhr.responseText)); }
                    catch (e) { reject(new Error('json parse error')); }
                } else {
                    reject(new Error(xhr.responseText || 'HTTP ' + xhr.status));
                }
            });
            xhr.addEventListener('error', function () { reject(new Error('network error')); });
            xhr.send(fd);
        });
    }

    // -- html escape --

    function esc(ch) {
        if (ch === '&') return '&amp;';
        if (ch === '<') return '&lt;';
        if (ch === '>') return '&gt;';
        return ch;
    }

    // -- render ascii text --

    function renderText(data) {
        if (!data || !data.text) return;
        placeholder.style.display = 'none';
        asciiOutput.style.display = 'block';

        if (data.colors) {
            var binary = atob(data.colors);
            var colors = new Uint8Array(binary.length);
            for (var i = 0; i < binary.length; i++) colors[i] = binary.charCodeAt(i);

            var lines = data.text.split('\n');
            var parts = [];
            var ci = 0;

            for (var y = 0; y < lines.length; y++) {
                var line = lines[y];
                if (!line.length) continue;
                for (var x = 0; x < line.length; x++) {
                    var idx = ci * 3;
                    parts.push('<span style="color:rgb(' +
                        (colors[idx] || 0) + ',' + (colors[idx + 1] || 0) + ',' + (colors[idx + 2] || 0) +
                        ')">' + esc(line[x]) + '</span>');
                    ci++;
                }
                parts.push('\n');
            }
            asciiOutput.innerHTML = parts.join('');
        } else {
            asciiOutput.textContent = data.text;
        }

        lastAsciiText = data.text;
        downloadBtn.disabled = false;
    }

    // render from buffer (no DOM rebuild for plain text)
    function renderBufferedFrame(idx) {
        var f = frameBuffer[idx];
        if (!f) return;
        placeholder.style.display = 'none';
        asciiOutput.style.display = 'block';

        if (f.html) {
            asciiOutput.innerHTML = f.html;
        } else {
            asciiOutput.textContent = f.text;
        }

        lastAsciiText = f.text;
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

    function updateFrameUI() {
        frameSlider.value = currentFrame;
        frameInfo.textContent = (currentFrame + 1) + '/' + totalFrames;
        var buffered = frameBuffer.filter(function (f) { return f !== null && f !== undefined; }).length;
        bufferInfoEl.textContent = 'bufor: ' + buffered + '/' + totalFrames + ' klatek';
    }

    // ====== IMAGE ======

    function convertImage() {
        if (!currentFile || converting) return;
        converting = true;
        convertBtn.disabled = true;
        convertBtn.textContent = '...';
        showProgress('upload...', 0);

        var settings = getSettings();
        var fd = buildFormData(currentFile, settings);
        var start = performance.now();

        uploadXHR('/api/convert/image', fd, function (pct) {
            showProgress('upload ' + Math.round(pct * 100) + '%', pct * 100);
            if (pct >= 1) showProgress('przetwarzam...', -1);
        }).then(function (data) {
            hideProgress();
            renderText(data);
            updateStats(data, performance.now() - start);
        }).catch(function (e) {
            hideProgress();
            appendLog({ timestamp: timeNow(), level: 'error', message: 'konwersja: ' + e.message });
        }).finally(function () {
            converting = false;
            convertBtn.disabled = false;
            convertBtn.textContent = 'konwertuj';
        });
    }

    // ====== VIDEO ======

    function uploadVideo() {
        if (!currentFile || converting) return;
        converting = true;
        convertBtn.disabled = true;
        convertBtn.textContent = '...';
        showProgress('upload wideo...', 0);

        var settings = getSettings();
        var fd = buildFormData(currentFile, settings);

        uploadXHR('/api/convert/video', fd, function (pct) {
            showProgress('upload ' + Math.round(pct * 100) + '%', pct * 100);
            if (pct >= 1) showProgress('analiza wideo...', -1);
        }).then(function (data) {
            videoSessionId = data.sessionId;
            totalFrames = data.totalFrames;
            videoFps = data.fps || 30;
            currentFrame = 0;
            preloaded = false;
            frameBuffer = new Array(totalFrames);

            frameSlider.max = totalFrames - 1;
            frameSlider.value = 0;
            videoMeta.textContent = data.width + 'x' + data.height + ' | ' +
                videoFps.toFixed(1) + 'fps | ' +
                data.duration.toFixed(1) + 's | ' +
                totalFrames + ' klatek';
            videoControls.style.display = 'block';
            updateFrameUI();

            converting = false;
            convertBtn.disabled = false;
            convertBtn.textContent = 'konwertuj';

            // rozpocznij preload klatek w tle
            startPreload();
        }).catch(function (e) {
            hideProgress();
            appendLog({ timestamp: timeNow(), level: 'error', message: 'upload wideo: ' + e.message });
            converting = false;
            convertBtn.disabled = false;
            convertBtn.textContent = 'konwertuj';
        });
    }

    // preload raw frames na serwerze (jeden ffmpeg pass)
    function startPreload() {
        showProgress('ekstrakcja klatek...', 0);

        var fd = new FormData();
        fd.append('sessionId', videoSessionId);

        fetch('/api/convert/preload', { method: 'POST', body: fd })
            .then(function (resp) {
                if (!resp.ok) return resp.text().then(function (t) { throw new Error(t); });
                return resp.json();
            })
            .then(function (data) {
                preloaded = true;
                appendLog({ timestamp: timeNow(), level: 'info', message: 'preload: ' + data.extracted + '/' + data.total + ' klatek' });

                // po preload - załaduj pierwszą klatkę i zacznij buforować
                showProgress('konwersja ascii...', 0);
                return bufferFrames(0, Math.min(bufferBatchSize, totalFrames));
            })
            .then(function () {
                hideProgress();
                if (frameBuffer[0]) {
                    renderBufferedFrame(0);
                    updateStats(frameBuffer[0]);
                }
                updateFrameUI();

                // kontynuuj buforowanie w tle
                bufferRemaining(bufferBatchSize);
            })
            .catch(function (e) {
                hideProgress();
                appendLog({ timestamp: timeNow(), level: 'error', message: 'preload: ' + e.message });
                // fallback - ładuj klatki na żądanie
                preloaded = false;
                loadSingleFrame(0);
            });
    }

    // buforuj batch klatek (konwersja ascii na serwerze)
    function bufferFrames(startFrame, count) {
        if (bufferingFrom === startFrame) return bufferingPromise;

        var settings = getSettings();
        var fd = buildFormData(null, settings, {
            sessionId: videoSessionId,
            startFrame: startFrame,
            count: count
        });

        bufferingFrom = startFrame;

        bufferingPromise = fetch('/api/convert/batch', { method: 'POST', body: fd })
            .then(function (resp) {
                if (!resp.ok) return resp.text().then(function (t) { throw new Error(t); });
                return resp.json();
            })
            .then(function (frames) {
                for (var i = 0; i < frames.length; i++) {
                    var f = frames[i];
                    var html = null;

                    if (f.colors) {
                        var binary = atob(f.colors);
                        var colors = new Uint8Array(binary.length);
                        for (var j = 0; j < binary.length; j++) colors[j] = binary.charCodeAt(j);

                        var lines = f.text.split('\n');
                        var parts = [];
                        var ci = 0;
                        for (var y = 0; y < lines.length; y++) {
                            var line = lines[y];
                            if (!line.length) continue;
                            for (var x = 0; x < line.length; x++) {
                                var idx2 = ci * 3;
                                parts.push('<span style="color:rgb(' +
                                    (colors[idx2] || 0) + ',' + (colors[idx2 + 1] || 0) + ',' + (colors[idx2 + 2] || 0) +
                                    ')">' + esc(line[x]) + '</span>');
                                ci++;
                            }
                            parts.push('\n');
                        }
                        html = parts.join('');
                    }

                    frameBuffer[f.frameNumber] = {
                        text: f.text,
                        html: html,
                        columns: f.columns,
                        rows: f.rows,
                        colors: !!f.colors,
                        frameNumber: f.frameNumber,
                        totalFrames: f.totalFrames
                    };
                }

                var pct = Math.round((startFrame + frames.length) / totalFrames * 100);
                showProgress('konwersja ascii ' + pct + '%', pct);
                updateFrameUI();

                bufferingFrom = -1;
                bufferingPromise = null;
                return frames.length;
            })
            .catch(function (e) {
                bufferingFrom = -1;
                bufferingPromise = null;
                appendLog({ timestamp: timeNow(), level: 'error', message: 'batch: ' + e.message });
                return 0;
            });

        return bufferingPromise;
    }

    // buforuj resztę klatek w tle
    function bufferRemaining(from) {
        if (from >= totalFrames) {
            hideProgress();
            updateFrameUI();
            return;
        }

        var count = Math.min(bufferBatchSize, totalFrames - from);
        bufferFrames(from, count).then(function (got) {
            if (got > 0) {
                setTimeout(function () { bufferRemaining(from + got); }, 10);
            } else {
                hideProgress();
            }
        });
    }

    // fallback: ładuj jedną klatkę (bez preload)
    function loadSingleFrame(frameNumber) {
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
                if (!resp.ok) return resp.text().then(function (t) { throw new Error(t); });
                return resp.json();
            })
            .then(function (data) {
                currentFrame = data.frameNumber;
                renderText(data);
                updateStats(data, performance.now() - start);
                updateFrameUI();
                hideProgress();
            })
            .catch(function (e) {
                hideProgress();
                appendLog({ timestamp: timeNow(), level: 'error', message: 'klatka: ' + e.message });
            });
    }

    // -- wyświetl klatkę (z bufora lub fallback) --

    function showFrame(idx) {
        idx = Math.max(0, Math.min(idx, totalFrames - 1));
        currentFrame = idx;

        if (frameBuffer[idx]) {
            renderBufferedFrame(idx);
            updateStats(frameBuffer[idx]);
            updateFrameUI();
        } else {
            loadSingleFrame(idx);
        }
    }

    // ====== PLAYBACK ======

    function startPlayback() {
        if (isPlaying || !videoSessionId) return;
        if (currentFrame >= totalFrames - 1) currentFrame = 0;

        isPlaying = true;
        playBtn.textContent = '⏸';
        playBtn.classList.add('playing');

        var lastTime = performance.now();
        var frameInterval = 1000 / (videoFps * playSpeed);

        function tick(now) {
            if (!isPlaying) return;

            var delta = now - lastTime;
            if (delta >= frameInterval) {
                lastTime = now - (delta % frameInterval);

                currentFrame++;
                if (currentFrame >= totalFrames) {
                    stopPlayback();
                    return;
                }

                if (frameBuffer[currentFrame]) {
                    renderBufferedFrame(currentFrame);
                    updateFrameUI();
                } else {
                    // nie ma w buforze - czekaj
                    currentFrame--;
                    updateFrameUI();
                }
            }

            playRafId = requestAnimationFrame(tick);
        }

        playRafId = requestAnimationFrame(tick);
    }

    function stopPlayback() {
        isPlaying = false;
        playBtn.textContent = '▶ play';
        playBtn.classList.remove('playing');
        if (playRafId) {
            cancelAnimationFrame(playRafId);
            playRafId = null;
        }
        updateFrameUI();
    }

    // -- download --

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

    // -- re-buforuj z nowymi ustawieniami --

    function rebufferVideo() {
        if (!videoSessionId || !preloaded) return;
        // wyczyść bufor
        frameBuffer = new Array(totalFrames);
        stopPlayback();
        showProgress('re-konwersja...', 0);
        bufferFrames(0, Math.min(bufferBatchSize, totalFrames)).then(function () {
            if (frameBuffer[currentFrame]) {
                renderBufferedFrame(currentFrame);
                updateStats(frameBuffer[currentFrame]);
            }
            bufferRemaining(bufferBatchSize);
        });
    }

    // ====== EVENT LISTENERS ======

    fileInput.addEventListener('change', function (e) {
        currentFile = e.target.files[0];
        if (!currentFile) { convertBtn.disabled = true; return; }

        var ext = currentFile.name.split('.').pop().toLowerCase();
        isVideo = ['mp4', 'avi', 'mkv', 'webm', 'mov', 'flv', 'wmv'].indexOf(ext) !== -1;
        var mb = (currentFile.size / 1048576).toFixed(2);
        fileInfo.textContent = currentFile.name + ' (' + mb + ' MB)' + (isVideo ? ' 🎬' : ' 🖼️');

        videoSessionId = null;
        videoControls.style.display = 'none';
        stopPlayback();
        frameBuffer = [];
        preloaded = false;
        convertBtn.disabled = false;
    });

    convertBtn.addEventListener('click', function () {
        if (isVideo) uploadVideo();
        else convertImage();
    });

    downloadBtn.addEventListener('click', downloadTxt);

    // suwaki
    stepRange.addEventListener('input', function () { stepValue.textContent = stepRange.value; });
    maxColsRange.addEventListener('input', function () { maxColsValue.textContent = maxColsRange.value; });
    thresholdRange.addEventListener('input', function () { thresholdValue.textContent = thresholdRange.value; });
    fontSizeRange.addEventListener('input', function () {
        fontSizeValue.textContent = fontSizeRange.value;
        asciiOutput.style.fontSize = fontSizeRange.value + 'px';
        asciiOutput.style.lineHeight = Math.round(parseInt(fontSizeRange.value) * 1.1) + 'px';
    });
    speedRange.addEventListener('input', function () {
        playSpeed = parseInt(speedRange.value) / 10;
        speedValue.textContent = playSpeed.toFixed(1);
    });

    // zmiana efektu -> re-buforuj
    effectSelect.addEventListener('change', function () {
        if (isVideo && videoSessionId && preloaded) {
            rebufferVideo();
        } else if (!isVideo && currentFile && lastAsciiText) {
            convertImage();
        }
    });

    // video nav
    prevFrameBtn.addEventListener('click', function () {
        stopPlayback();
        if (currentFrame > 0) showFrame(currentFrame - 1);
    });

    nextFrameBtn.addEventListener('click', function () {
        stopPlayback();
        if (currentFrame < totalFrames - 1) showFrame(currentFrame + 1);
    });

    playBtn.addEventListener('click', function () {
        if (isPlaying) stopPlayback();
        else startPlayback();
    });

    stopBtn.addEventListener('click', function () {
        stopPlayback();
        currentFrame = 0;
        showFrame(0);
    });

    frameSlider.addEventListener('input', function () {
        stopPlayback();
        currentFrame = parseInt(frameSlider.value);
        showFrame(currentFrame);
    });

    // logi
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

    // keyboard
    document.addEventListener('keydown', function (e) {
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'SELECT') return;

        if (isVideo && videoSessionId) {
            if (e.key === 'ArrowLeft') {
                e.preventDefault();
                stopPlayback();
                if (currentFrame > 0) showFrame(currentFrame - 1);
            } else if (e.key === 'ArrowRight') {
                e.preventDefault();
                stopPlayback();
                if (currentFrame < totalFrames - 1) showFrame(currentFrame + 1);
            } else if (e.key === ' ') {
                e.preventDefault();
                if (isPlaying) stopPlayback();
                else startPlayback();
            }
        }
    });

    // init
    initSignalR();
})();
