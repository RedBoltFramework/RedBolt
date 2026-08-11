const sampleElement = document.getElementById('sample');
const samples = Array.from(sampleElement.options,
    option => [option.value, option.text, option.dataset.application]);
const selectedSample = new URLSearchParams(location.search).get('sample');
const selectedEntry = samples.find(([id]) => id === selectedSample);
const canvasElement = document.getElementById('canvas');
const statusElement = document.getElementById('status');
const progressElement = document.getElementById('progress');
const fullscreenButton = document.getElementById('fullscreen-button');
const devicePixelRatio = window.devicePixelRatio || 1;
let canvasWidth = 0;
let canvasHeight = 0;
let resizeHandler;
let fullscreenResizeHandler;
let resizeTimeout = false;
let totalDependencies = 0;
let moduleConfig;
let networkInterop;
const networkConnections = new Map();
const networkChannelIndexes = new Map([
    ['uu', 0],
    ['ru', 1],
    ['uo', 2],
    ['ro', 3]
]);

function encodeSignal(...values) {
    const encoder = new TextEncoder();
    const encoded = values.map(value => encoder.encode(value));
    const result = new Uint8Array(encoded.reduce((size, value) => size + value.length + 1, 0));
    let offset = 0;
    for (const value of encoded) {
        result.set(value, offset);
        offset += value.length + 1;
    }
    return result;
}

function decodeSignal(value) {
    const bytes = new Uint8Array(value);
    const decoder = new TextDecoder();
    const result = [];
    let begin = 0;
    for (let end = 0; end <= bytes.length; ++end) {
        if (end === bytes.length || bytes[end] === 0) {
            result.push(decoder.decode(bytes.subarray(begin, end)));
            begin = end + 1;
        }
    }
    return result;
}

function bytesToBase64(bytes) {
    let binary = '';
    const chunkSize = 0x8000;
    for (let offset = 0; offset < bytes.length; offset += chunkSize)
        binary += String.fromCharCode(...bytes.subarray(offset, offset + chunkSize));
    return btoa(binary);
}

function base64ToBytes(value) {
    const binary = atob(value);
    const result = new Uint8Array(binary.length);
    for (let index = 0; index < binary.length; ++index)
        result[index] = binary.charCodeAt(index);
    return result;
}

function closeNetworkConnection(id, error = '') {
    const state = networkConnections.get(id);
    if (!state || state.closed)
        return;

    state.closed = true;
    state.signalingComplete = true;
    networkConnections.delete(id);
    for (const channel of state.channels) {
        if (!channel)
            continue;
        channel.onopen = null;
        channel.onclose = null;
        channel.onmessage = null;
        channel.close();
    }
    state.peer.ondatachannel = null;
    state.peer.onconnectionstatechange = null;
    state.peer.close();
    state.socket.onclose = null;
    state.socket.close();
    networkInterop.Disconnected(id, error);
}

function initializeNetworkChannel(state, channel) {
    const index = networkChannelIndexes.get(channel.label);
    if (index === undefined) {
        channel.close();
        return;
    }

    state.channels[index] = channel;
    channel.binaryType = 'arraybuffer';
    channel.onopen = () => {
        if (state.connected || !state.channels.every(item => item?.readyState === 'open'))
            return;
        state.connected = true;
        state.signalingComplete = true;
        state.socket.close();
        networkInterop.Connected(state.id);
    };
    channel.onclose = () => closeNetworkConnection(
        state.id, state.connected ? '' : 'A WebRTC data channel closed before connecting.');
    channel.onmessage = async event => {
        const value = event.data instanceof Blob ? await event.data.arrayBuffer() : event.data;
        const bytes = value instanceof ArrayBuffer
            ? new Uint8Array(value)
            : new Uint8Array(value.buffer, value.byteOffset, value.byteLength);
        networkInterop.Data(state.id, bytesToBase64(bytes));
    };
}

async function handleNetworkSignal(state, event) {
    const value = event.data instanceof Blob ? await event.data.arrayBuffer() : event.data;
    const [type, first = '', second = ''] = decodeSignal(value);
    if (type === 'offer' || type === 'answer') {
        await state.peer.setRemoteDescription({ type, sdp: first });
        if (type === 'offer') {
            const answer = await state.peer.createAnswer();
            await state.peer.setLocalDescription(answer);
            state.socket.send(encodeSignal('answer', state.peer.localDescription.sdp));
        }
    } else if (type === 'candidate') {
        await state.peer.addIceCandidate({ candidate: first, sdpMid: second || null });
    }
}

function connectNetwork(id, url, iceServers) {
    try {
        const peer = new RTCPeerConnection({
            iceServers: iceServers
                ? iceServers.split('\n').filter(value => value).map(urls => ({ urls }))
                : []
        });
        const socket = new WebSocket(url);
        socket.binaryType = 'arraybuffer';
        const state = {
            id,
            peer,
            socket,
            channels: new Array(4),
            signalQueue: Promise.resolve(),
            connected: false,
            signalingComplete: false,
            closed: false
        };
        networkConnections.set(id, state);

        peer.ondatachannel = event => initializeNetworkChannel(state, event.channel);
        peer.onicecandidate = event => {
            if (event.candidate && socket.readyState === WebSocket.OPEN)
                socket.send(encodeSignal(
                    'candidate', event.candidate.candidate, event.candidate.sdpMid || ''));
        };
        peer.onconnectionstatechange = () => {
            if (peer.connectionState === 'failed' || peer.connectionState === 'closed')
                closeNetworkConnection(id, `WebRTC connection ${peer.connectionState}.`);
        };
        socket.onmessage = event => {
            state.signalQueue = state.signalQueue
                .then(() => handleNetworkSignal(state, event))
                .catch(error => closeNetworkConnection(id, String(error)));
        };
        socket.onclose = () => {
            if (!state.signalingComplete)
                closeNetworkConnection(id, 'The signaling WebSocket closed before WebRTC connected.');
        };
        socket.onerror = () => {
            if (!state.signalingComplete)
                closeNetworkConnection(id, 'The signaling WebSocket failed.');
        };
        return true;
    } catch (error) {
        console.error(error);
        return false;
    }
}

function sendNetworkData(id, packetType, payload) {
    const channel = networkConnections.get(id)?.channels[packetType];
    if (channel?.readyState !== 'open')
        return false;
    channel.send(base64ToBytes(payload));
    return true;
}

function calculateCanvasSize() {
    const style = window.getComputedStyle(canvasElement);
    canvasWidth = Math.max(1, Math.round(Number.parseFloat(style.getPropertyValue('width')) * devicePixelRatio));
    canvasHeight = Math.max(1, Math.round(Number.parseFloat(style.getPropertyValue('height')) * devicePixelRatio));
}

function isFullScreen() {
    return !!(document.fullscreenElement
        || document.webkitFullscreenElement
        || document.mozFullScreenElement
        || document.msFullscreenElement);
}

async function viewportResizeHandler() {
    if (document.hidden || !resizeHandler)
        return;

    calculateCanvasSize();
    if (isFullScreen())
        await fullscreenResizeHandler(
            screen.width * devicePixelRatio, screen.height * devicePixelRatio, devicePixelRatio);
    else
        await resizeHandler(canvasWidth, canvasHeight, devicePixelRatio);
}

function visibilityChanged() {
    if (!document.hidden)
        setTimeout(viewportResizeHandler, 100);
}

function handleMouseDown() {
    window.focus();
}

function enterFullscreen(show) {
    if (show === undefined)
        show = !isFullScreen();

    if (show) {
        if (canvasElement.requestFullscreen)
            canvasElement.requestFullscreen();
        else if (canvasElement.webkitRequestFullScreen)
            canvasElement.webkitRequestFullScreen();
        else if (canvasElement.mozRequestFullScreen)
            canvasElement.mozRequestFullScreen();
        else if (canvasElement.msRequestFullscreen)
            canvasElement.msRequestFullscreen();
    } else {
        if (document.exitFullscreen)
            document.exitFullscreen();
        else if (document.webkitExitFullscreen)
            document.webkitExitFullscreen();
        else if (document.mozCancelFullScreen)
            document.mozCancelFullScreen();
        else if (document.msExitFullscreen)
            document.msExitFullscreen();
    }
}

function setStatus(text) {
    if (setStatus.interval)
        clearInterval(setStatus.interval);

    const match = text.match(/([^(]+)\((\d+(\.\d+)?)\/(\d+)\)/);
    if (match) {
        text = match[1];
        progressElement.value = Number.parseInt(match[2]) * 100;
        progressElement.max = Number.parseInt(match[4]) * 100;
        progressElement.hidden = false;
    } else {
        progressElement.removeAttribute('value');
        progressElement.hidden = true;
    }
    statusElement.innerText = text;
}

function ready() {
    document.getElementById('loading').hidden = true;
    if (!document.referrer.includes('itch.io'))
        fullscreenButton.hidden = false;
    if (!document.hidden)
        setTimeout(viewportResizeHandler, 100);
}

function monitorRunDependencies(left) {
    totalDependencies = Math.max(totalDependencies, left);
    setStatus(left
        ? `Preparing... (${totalDependencies - left}/${totalDependencies})`
        : 'All downloads complete.');
}

function initializePersistentStorage(runtimeModule) {
    runtimeModule.addRunDependency('IndexedDB');
    runtimeModule.FS.mkdir('/IndexedDB');
    runtimeModule.FS.mount(runtimeModule.FS.filesystems.IDBFS, { autoPersist: true }, '/IndexedDB');
    runtimeModule.FS.syncfs(true, error => {
        if (error)
            console.error(error);
        runtimeModule.removeRunDependency('IndexedDB');
    });
}

document.addEventListener('fullscreenchange', viewportResizeHandler, false);
document.addEventListener('mozfullscreenchange', viewportResizeHandler, false);
document.addEventListener('webkitfullscreenchange', viewportResizeHandler, false);
document.addEventListener('MSFullscreenChange', viewportResizeHandler, false);
document.addEventListener('visibilitychange', visibilityChanged, false);
document.addEventListener('msvisibilitychange', visibilityChanged, false);
document.addEventListener('webkitvisibilitychange', visibilityChanged, false);
document.addEventListener('mousedown', handleMouseDown, false);
canvasElement.addEventListener('contextmenu', event => event.preventDefault());
window.addEventListener('resize', event => {
    if (resizeTimeout)
        clearTimeout(resizeTimeout);
    resizeTimeout = setTimeout(() => viewportResizeHandler(event), 1000);
});
fullscreenButton.addEventListener('click', () => enterFullscreen());

if (!selectedEntry) {
    document.getElementById('load').addEventListener('click', () => {
        location.search = new URLSearchParams({ sample: sampleElement.value });
    });
    await new Promise(() => {});
}

document.title = `${selectedEntry[1]} — RedBolt`;
document.getElementById('selector').hidden = true;
document.getElementById('runner').classList.add('active');
calculateCanvasSize();
setStatus('Downloading...');

const { dotnet } = await import('./_framework/dotnet.js');
const vfs = await fetch('./resources.manifest.json').then(response => response.json());
setStatus('Preparing...');
moduleConfig = {
    canvas: canvasElement,
    print: text => console.log(text),
    printErr: text => console.error(text),
    monitorRunDependencies,
    preRun: [initializePersistentStorage]
};
const { getAssemblyExports, setModuleImports, runMain } = await dotnet
    .withConfig({ resources: { vfs } })
    .withApplicationArgumentsFromQuery()
    .withModuleConfig(moduleConfig)
    .create();
const coreExports = await getAssemblyExports('RedBolt.Core.dll');
const applicationExports = coreExports.RedBolt.Engine.Application;
const networkExports = await getAssemblyExports('RedBolt.Network.dll');
networkInterop = networkExports.RedBolt.Network.BrowserDataChannelInterop;

setModuleImports('main.js', {
    dom: {
        getSelectedApplicationType: () => selectedEntry[2],
        setResult: (message, success) => {
            const result = document.getElementById('result');
            result.innerText = message;
            result.dataset.status = success ? 'success' : 'failure';
        }
    },
    canvas: {
        getWidth: () => canvasWidth,
        getHeight: () => canvasHeight,
        setResizeHandlers: () => {
            resizeHandler = (width, height, dpiScale) =>
                applicationExports.ResizeCanvasAsync(width, height, false, dpiScale);
            fullscreenResizeHandler = (width, height, dpiScale) =>
                applicationExports.ResizeCanvasAsync(width, height, true, dpiScale);
        },
        setRendererSize: (width, height) => {
            console.log('Engine renderer size changed to', width, height);
            calculateCanvasSize();
            if (document.hidden)
                return;

            canvasElement.width = width;
            canvasElement.height = height;
            if (canvasWidth === width && canvasHeight === height)
                return;

            console.log('Renderer and canvas resolution mismatch, updating renderer resolution',
                canvasWidth, canvasHeight);
            if (resizeHandler)
                resizeHandler(canvasWidth, canvasHeight, devicePixelRatio).catch(error => console.error(error));
        }
    },
    mainLoop: {
        set: () => {
            const runFrame = async () => {
                await applicationExports.RunFrameAsync();
                requestAnimationFrame(runFrame);
            };
            requestAnimationFrame(runFrame);
        }
    },
    network: {
        connect: connectNetwork,
        send: sendNetworkData,
        close: id => closeNetworkConnection(id)
    }
});

await runMain();
setStatus('Running...');
ready();
