// ascier web - real-time ascii video streaming
// signalr streaming + bufor klatek + raf playback
(function () {
    'use strict';

    // ===== DOM =====
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
    var statusText = $('statusText');
    var toastContainer = $('toastContainer');
    var limitsInfo = $('limitsInfo');

    // limity (domyślne, pobierane z serwera)
    var limits = { maxVideoSizeMb: 50, maxImageSizeMb: 20, maxVideoDurationS: 60, maxVideoResolution: 1280 };

    // ===== STATE =====
    var file = null;
    var isVideo = false;
    var sessionId = null;
    var totalFrames = 0;
    var fps = 30;
    var currentFrame = 0;
    var playing = false;
    var streaming = false;
    var converting = false;
    var speed = 1.0;
    var lastAscii = '';

    // bufor klatek
    var frameBuffer = [];
    var lastRenderedIdx = -1;

    // signalr stream subscription
    var streamSub = null;

    // playback RAF
    var rafId = null;
    var lastTickTime = 0;
    var playAccum = 0;

    // signalr connection
    var connection = null;
    var logCount = 0;
    var logOpen = false;

    // ===== TOAST =====

    function showToast(msg, type) {
        type = type || 'error';
        var toast = document.createElement('div');
        toast.className = 'toast toast-' + type;
        toast.textContent = msg;
        toastContainer.appendChild(toast);
        setTimeout(function () { toast.classList.add('toast-visible'); }, 10);
        setTimeout(function () {
            toast.classList.remove('toast-visible');
            setTimeout(function () { toast.remove(); }, 300);
        }, 5000);
    }

    // ===== SIGNALR =====

    function initSignalR() {
        connection = new signalR.HubConnectionBuilder()
            .withUrl('/hub/conversion')
            .withAutomaticReconnect()
            .build();

        connection.on('NewLog', appendLog);
        connection.on('Error', function (m) {
            appendLog({ timestamp: timeNow(), level: 'error', message: m });
        });

        connection.start().then(function () {
            connection.invoke('SubscribeLogs').catch(function () {});
        }).catch(function (err) {
            appendLog({ timestamp: timeNow(), level: 'warn', message: 'signalr: ' + err.message });
        });
    }

    // ===== STREAMING =====

    function startStream() {
        if (!sessionId || !connection || connection.state !== 'Connected') return;

        cancelStream();
        streaming = true;
        frameBuffer = new Array(totalFrames);
        lastRenderedIdx = -1;

        var s = getSettings();

        streamSub = connection.stream('StreamVideo',
            sessionId, s.effect, s.step, s.colorMode,
            s.threshold, s.invert, s.maxColumns
        ).subscribe({
            next: function (frame) {
                storeFrame(frame);

                // wyświetl pierwszą klatkę automatycznie
                if (frame.frameNumber === 0 && !playing) {
                    renderFrame(0);
                    updateStats(frame);
                }

                // progress
                var done = frame.frameNumber + 1;
                var pct = (done / frame.totalFrames * 100) | 0;
                showProgress('streaming ' + done + '/' + frame.totalFrames + ' (' + pct + '%)', pct);
                updateBufferUI();
            },
            complete: function () {
                streaming = false;
                hideProgress();
                updateBufferUI();
                var count = bufferedCount();
                appendLog({ timestamp: timeNow(), level: 'info',
                    message: 'stream gotowy: ' + count + '/' + totalFrames + ' klatek' });
                if (count < totalFrames && count > 0) {
                    showToast('stream przerwany: ' + count + '/' + totalFrames + ' klatek', 'warn');
                } else if (count === 0) {
                    showToast('stream nie zwrócił żadnych klatek', 'error');
                }
            },
            error: function (err) {
                streaming = false;
                hideProgress();
                var msg = err.message || String(err);
                appendLog({ timestamp: timeNow(), level: 'error',
                    message: 'stream: ' + msg });
                showToast('błąd streamu: ' + msg, 'error');
            }
        });
    }

    function cancelStream() {
        if (streamSub) {
            streamSub.dispose();
            streamSub = null;
        }
        streaming = false;
    }

    function storeFrame(frame) {
        var entry = {
            text: frame.text,
            columns: frame.columns,
            rows: frame.rows,
            frameNumber: frame.frameNumber,
            totalFrames: frame.totalFrames,
            hasColors: !!frame.colors,
            html: null
        };

        // pre-build HTML dla trybu kolorowego (bit operacje na indeksach)
        if (frame.colors) {
            entry.html = buildColorHtml(frame.text, frame.colors);
        }

        frameBuffer[frame.frameNumber] = entry;
    }

    // ===== RENDERING =====

    // budowanie html z kolorami - operacje na uint8array
    function buildColorHtml(text, colorsB64) {
        var bin = atob(colorsB64);
        var len = bin.length;
        var colors = new Uint8Array(len);
        for (var i = 0; i < len; i++) colors[i] = bin.charCodeAt(i);

        var lines = text.split('\n');
        // pre-alokuj tablicę wynikową
        var parts = [];
        var ci = 0;

        for (var y = 0; y < lines.length; y++) {
            var line = lines[y];
            if (!line.length) continue;
            for (var x = 0; x < line.length; x++) {
                // optymalizacja: indeks koloru = ci * 3 -> (ci << 1) + ci
                var idx = (ci << 1) + ci;
                var ch = line.charCodeAt(x);
                // inline html escape z operacjami bitowymi
                var esc;
                if (ch === 38) esc = '&amp;';
                else if (ch === 60) esc = '&lt;';
                else if (ch === 62) esc = '&gt;';
                else esc = line[x];

                parts.push('<span style="color:rgb(');
                parts.push(colors[idx] | 0);
                parts.push(',');
                parts.push(colors[idx + 1] | 0);
                parts.push(',');
                parts.push(colors[idx + 2] | 0);
                parts.push(')">');
                parts.push(esc);
                parts.push('</span>');
                ci++;
            }
            parts.push('\n');
        }

        return parts.join('');
    }

    // render klatki z bufora (unika redundantnych DOM updates)
    function renderFrame(idx) {
        if (idx === lastRenderedIdx) return;
        var f = frameBuffer[idx];
        if (!f) return;

        lastRenderedIdx = idx;
        placeholder.style.display = 'none';
        asciiOutput.style.display = 'block';

        if (f.html) {
            asciiOutput.innerHTML = f.html;
        } else {
            // monochromatyczny - textContent = zero DOM overhead
            asciiOutput.textContent = f.text;
        }

        lastAscii = f.text;
        currentFrame = idx;
        downloadBtn.disabled = false;
    }

    // render z danych serwera (obraz lub seek frame)
    function renderServerFrame(data) {
        placeholder.style.display = 'none';
        asciiOutput.style.display = 'block';

        if (data.colors) {
            asciiOutput.innerHTML = buildColorHtml(data.text, data.colors);
        } else {
            asciiOutput.textContent = data.text;
        }

        lastAscii = data.text;
        downloadBtn.disabled = false;
    }

    // ===== PLAYBACK =====

    function startPlayback() {
        if (playing || totalFrames < 2) return;
        if (currentFrame >= totalFrames - 1) currentFrame = 0;

        playing = true;
        playBtn.textContent = '\u23F8';
        playBtn.classList.add('playing');

        lastTickTime = performance.now();
        playAccum = 0;
        rafId = requestAnimationFrame(playTick);
    }

    function stopPlayback() {
        playing = false;
        playBtn.textContent = '\u25B6';
        playBtn.classList.remove('playing');
        if (rafId) {
            cancelAnimationFrame(rafId);
            rafId = null;
        }
    }

    function playTick(now) {
        if (!playing) return;

        var delta = now - lastTickTime;
        lastTickTime = now;
        playAccum += delta;

        var frameTime = 1000 / (fps * speed);
        var advanced = false;

        // obsługuj wiele klatek na tick (szybki playback / wolny komputer)
        while (playAccum >= frameTime) {
            playAccum -= frameTime;

            var next = currentFrame + 1;
            if (next >= totalFrames) {
                stopPlayback();
                updateFrameUI();
                return;
            }

            if (frameBuffer[next]) {
                currentFrame = next;
                advanced = true;
            } else {
                // buffer underrun - czekaj na więcej klatek
                playAccum = 0;
                break;
            }
        }

        if (advanced) {
            renderFrame(currentFrame);
            updateFrameUI();
        }

        rafId = requestAnimationFrame(playTick);
    }

    function showFrame(idx) {
        idx = Math.max(0, Math.min(idx, totalFrames - 1));
        currentFrame = idx;

        if (frameBuffer[idx]) {
            renderFrame(idx);
        } else {
            // fallback - pobierz z serwera
            fetchSingleFrame(idx);
        }
        updateFrameUI();
    }

    // fallback seek - pobierz jedną klatkę z /api/convert/frame
    function fetchSingleFrame(idx) {
        var s = getSettings();
        var fd = new FormData();
        fd.append('sessionId', sessionId);
        fd.append('frameNumber', idx);
        fd.append('effect', s.effect);
        fd.append('step', s.step);
        fd.append('colorMode', s.colorMode);
        fd.append('threshold', s.threshold);
        fd.append('invert', s.invert);
        fd.append('maxColumns', s.maxColumns);

        fetch('/api/convert/frame', { method: 'POST', body: fd })
            .then(function (r) {
                if (!r.ok) throw new Error('HTTP ' + r.status);
                return r.json();
            })
            .then(function (data) {
                renderServerFrame(data);
                currentFrame = data.frameNumber;
                updateFrameUI();
                updateStats(data);
            })
            .catch(function (e) {
                appendLog({ timestamp: timeNow(), level: 'error', message: 'seek: ' + e.message });
                showToast('nie udało się pobrać klatki ' + (idx + 1), 'warn');
            });
    }

    // ===== IMAGE =====

    function convertImage() {
        if (!file || converting) return;
        converting = true;
        convertBtn.disabled = true;
        convertBtn.textContent = '...';
        showProgress('upload...', 0);

        var s = getSettings();
        var fd = new FormData();
        fd.append('file', file);
        fd.append('effect', s.effect);
        fd.append('step', s.step);
        fd.append('colorMode', s.colorMode);
        fd.append('threshold', s.threshold);
        fd.append('invert', s.invert);
        fd.append('maxColumns', s.maxColumns);

        var t0 = performance.now();

        uploadXHR('/api/convert/image', fd, function (pct) {
            showProgress('upload ' + ((pct * 100) | 0) + '%', pct * 100);
            if (pct >= 1) showProgress('konwersja...', -1);
        }).then(function (data) {
            hideProgress();
            renderServerFrame(data);
            updateStats(data, performance.now() - t0);
        }).catch(function (e) {
            hideProgress();
            appendLog({ timestamp: timeNow(), level: 'error', message: 'obraz: ' + e.message });
        }).finally(function () {
            converting = false;
            convertBtn.disabled = false;
            convertBtn.textContent = 'konwertuj';
        });
    }

    // ===== VIDEO UPLOAD =====

    function uploadVideo() {
        if (!file || converting) return;
        converting = true;
        convertBtn.disabled = true;
        convertBtn.textContent = '...';
        showProgress('upload wideo...', 0);

        var s = getSettings();
        var fd = new FormData();
        fd.append('file', file);
        fd.append('effect', s.effect);
        fd.append('step', s.step);
        fd.append('colorMode', s.colorMode);
        fd.append('threshold', s.threshold);
        fd.append('invert', s.invert);
        fd.append('maxColumns', s.maxColumns);

        uploadXHR('/api/convert/video', fd, function (pct) {
            showProgress('upload ' + ((pct * 100) | 0) + '%', pct * 100);
            if (pct >= 1) showProgress('analiza...', -1);
        }).then(function (data) {
            sessionId = data.sessionId;
            totalFrames = data.totalFrames;
            fps = data.fps || 30;
            currentFrame = 0;

            frameSlider.max = totalFrames - 1;
            frameSlider.value = 0;
            videoMeta.textContent = data.width + 'x' + data.height +
                (data.effectiveWidth && data.effectiveWidth !== data.width
                    ? ' \u2192 ' + data.effectiveWidth + 'x' + data.effectiveHeight
                    : '') +
                ' | ' + (fps | 0) + 'fps | ' +
                data.duration.toFixed(1) + 's | ' +
                totalFrames + ' klatek';
            videoControls.style.display = 'block';
            updateFrameUI();

            converting = false;
            convertBtn.disabled = false;
            convertBtn.textContent = 'konwertuj';

            // start real-time streaming klatek ascii
            showProgress('streaming...', 0);
            startStream();
        }).catch(function (e) {
            hideProgress();
            appendLog({ timestamp: timeNow(), level: 'error', message: 'wideo: ' + e.message });
            converting = false;
            convertBtn.disabled = false;
            convertBtn.textContent = 'konwertuj';
        });
    }

    // ===== HELPERS =====

    function getSettings() {
        return {
            effect: effectSelect.value,
            step: parseInt(stepRange.value) | 0,
            colorMode: colorMode.checked,
            threshold: parseInt(thresholdRange.value) | 0,
            invert: invertMode.checked,
            maxColumns: parseInt(maxColsRange.value) | 0
        };
    }

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
                    catch (e) { reject(new Error('json parse')); }
                } else {
                    var msg = xhr.responseText || 'HTTP ' + xhr.status;
                    // serwer zwraca plain text w BadRequest
                    showToast(msg, 'error');
                    reject(new Error(msg));
                }
            });
            xhr.addEventListener('error', function () { reject(new Error('network')); });
            xhr.send(fd);
        });
    }

    function bufferedCount() {
        var c = 0;
        for (var i = 0; i < frameBuffer.length; i++) {
            if (frameBuffer[i]) c++;
        }
        return c;
    }

    function timeNow() {
        var d = new Date();
        return String(d.getHours()).padStart(2, '0') + ':' +
               String(d.getMinutes()).padStart(2, '0') + ':' +
               String(d.getSeconds()).padStart(2, '0') + '.' +
               String(d.getMilliseconds()).padStart(3, '0');
    }

    // ===== UI =====

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

    function updateFrameUI() {
        frameSlider.value = currentFrame;
        frameInfo.textContent = (currentFrame + 1) + '/' + totalFrames;
    }

    function updateBufferUI() {
        var count = bufferedCount();
        bufferInfoEl.textContent = 'bufor: ' + count + '/' + totalFrames + ' klatek';
    }

    function updateStats(data, ms) {
        var cols = data.columns | 0;
        var rows = data.rows | 0;
        var t = cols + '\u00D7' + rows + ' = ' + (cols * rows).toLocaleString() + ' znak\u00F3w';
        t += '\nefekt: ' + effectSelect.value;
        if (data.colors || data.hasColors) t += ' [kolor]';
        if (ms) t += '\nczas: ' + (ms | 0) + 'ms';
        if (data.totalFrames > 1) t += '\nklatka: ' + ((data.frameNumber | 0) + 1) + '/' + data.totalFrames;
        statsText.textContent = t;
    }

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

    function downloadTxt() {
        if (!lastAscii) return;
        var blob = new Blob([lastAscii], { type: 'text/plain;charset=utf-8' });
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = 'ascii_art.txt';
        a.click();
        URL.revokeObjectURL(url);
    }

    // ===== MONITORING =====

    function fetchStatus() {
        fetch('/api/status')
            .then(function (r) { return r.json(); })
            .then(function (s) {
                if (!statusText) return;
                statusText.textContent =
                    'uptime: ' + s.uptime +
                    '\nmem: ' + s.memoryMb + 'MB (gc: ' + s.gcMemoryMb + 'MB)' +
                    '\ncpu: ' + s.cpuSeconds + 's | thr: ' + s.threads +
                    '\nsesje: ' + s.activeSessions + ' | klatki: ' + s.totalFrames +
                    '\ngc: ' + s.gcCollections.join('/');
            })
            .catch(function () {});
    }

    // ===== EVENTS =====

    fileInput.addEventListener('change', function (e) {
        file = e.target.files[0];
        if (!file) { convertBtn.disabled = true; return; }

        var ext = file.name.split('.').pop().toLowerCase();
        isVideo = 'mp4 avi mkv webm mov flv wmv'.split(' ').indexOf(ext) !== -1;
        var mb = (file.size / 1048576).toFixed(2);
        fileInfo.textContent = file.name + ' (' + mb + 'MB)' + (isVideo ? ' \uD83C\uDFAC' : ' \uD83D\uDDBC\uFE0F');

        // walidacja rozmiaru
        var maxMb = isVideo ? limits.maxVideoSizeMb : limits.maxImageSizeMb;
        if (file.size > maxMb * 1048576) {
            showToast('plik za duży (' + mb + 'MB). limit: ' + maxMb + 'MB', 'error');
            fileInfo.textContent += ' ⛔ za duży!';
            convertBtn.disabled = true;
            file = null;
            return;
        }

        cancelStream();
        stopPlayback();
        sessionId = null;
        totalFrames = 0;
        frameBuffer = [];
        lastRenderedIdx = -1;
        videoControls.style.display = 'none';
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
        asciiOutput.style.lineHeight = ((parseInt(fontSizeRange.value) * 1.1) | 0) + 'px';
    });
    speedRange.addEventListener('input', function () {
        speed = parseInt(speedRange.value) / 10;
        speedValue.textContent = speed.toFixed(1);
    });

    // zmiana efektu -> re-stream dla video, re-konwersja dla obrazu
    effectSelect.addEventListener('change', function () {
        if (isVideo && sessionId) {
            stopPlayback();
            lastRenderedIdx = -1;
            frameBuffer = new Array(totalFrames);
            showProgress('re-konwersja...', 0);
            startStream();
        } else if (!isVideo && file && lastAscii) {
            convertImage();
        }
    });

    // zmiana ustawień konwersji -> re-stream dla video
    var settingsInputs = [stepRange, maxColsRange, thresholdRange, colorMode, invertMode];
    settingsInputs.forEach(function (el) {
        el.addEventListener('change', function () {
            if (isVideo && sessionId && !streaming) {
                stopPlayback();
                lastRenderedIdx = -1;
                frameBuffer = new Array(totalFrames);
                showProgress('re-konwersja...', 0);
                startStream();
            }
        });
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
        if (playing) stopPlayback();
        else startPlayback();
    });

    stopBtn.addEventListener('click', function () {
        stopPlayback();
        currentFrame = 0;
        showFrame(0);
    });

    frameSlider.addEventListener('input', function () {
        stopPlayback();
        showFrame(parseInt(frameSlider.value));
    });

    // logi
    logToggle.addEventListener('click', function () {
        logOpen = !logOpen;
        logBody.style.display = logOpen ? 'block' : 'none';
        logArrow.textContent = logOpen ? '\u25BC' : '\u25B2';
        if (logOpen) logBadge.classList.remove('error');
    });

    clearLogs.addEventListener('click', function () {
        logOutput.textContent = '';
        logCount = 0;
        logBadge.textContent = '0';
        logBadge.classList.remove('error');
    });

    // klawiatura
    document.addEventListener('keydown', function (e) {
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'SELECT') return;
        if (!isVideo || !sessionId) return;

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
            if (playing) stopPlayback();
            else startPlayback();
        }
    });

    // ===== INIT =====

    initSignalR();
    fetchStatus();
    setInterval(fetchStatus, 5000);

    // pobierz limity z serwera i wyświetl
    fetch('/api/limits').then(function (r) { return r.json(); }).then(function (l) {
        limits = l;
        limitsInfo.textContent = 'limity: wideo ' + l.maxVideoSizeMb + 'MB / ' +
            l.maxVideoDurationS + 's / ' + l.maxVideoResolution + 'px, obraz ' + l.maxImageSizeMb + 'MB';
    }).catch(function () {});
})();
