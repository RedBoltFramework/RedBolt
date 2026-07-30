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
    .withModuleConfig(moduleConfig)
    .create();
const platformExports = await getAssemblyExports('RedBolt.Platform.Web.dll');
const applicationExports = platformExports.RedBolt.Platform.Application;

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
    }
});

await runMain();
setStatus('Running...');
ready();
