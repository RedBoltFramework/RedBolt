const samples = [
    ['aggregated-input', 'Aggregated Input'],
    ['animating-scene', 'Animating Scene'],
    ['billboards', 'Billboards'],
    ['camera-operator', 'Camera Operator'],
    ['camera-shake', 'Camera Shake'],
    ['decals', 'Decals'],
    ['dynamic-geometry', 'Dynamic Geometry'],
    ['hello-gui', 'Hello GUI'],
    ['hello-world', 'Hello World'],
    ['huge-object-count', 'Huge Object Count'],
    ['input-logger', 'Input Logger'],
    ['light-animation', 'Light Animation'],
    ['material-animation', 'Material Animation'],
    ['multiple-viewports', 'Multiple Viewports'],
    ['pointer-adapter', 'Pointer Adapter'],
    ['render-to-texture', 'Render to Texture'],
    ['skeletal-animation', 'Skeletal Animation'],
    ['sound-effects', 'Sound Effects'],
    ['sound-synthesis', 'Sound Synthesis'],
    ['static-scene', 'Static Scene'],
    ['texture-formats', 'Texture Formats'],
    ['virtual-file-system', 'Virtual File System'],
    ['window-settings', 'Window Settings']
];
const selectedSample = new URLSearchParams(location.search).get('sample');
const selectedEntry = samples.find(([id]) => id === selectedSample);

if (!selectedEntry) {
    const select = document.getElementById('sample');
    for (const [id, title] of samples)
        select.add(new Option(title, id));
    document.getElementById('load').addEventListener('click', () => {
        location.search = new URLSearchParams({ sample: select.value });
    });
    await new Promise(() => {});
}

document.title = `${selectedEntry[1]} — RedBolt`;
document.getElementById('selector').hidden = true;
document.getElementById('runner').classList.add('active');

const { dotnet } = await import('./_framework/dotnet.js');
const vfs = await fetch('./resources.manifest.json').then(response => response.json());
const { setModuleImports, runMain } = await dotnet
    .withConfig({ resources: { vfs } })
    .withModuleConfig({ canvas: document.getElementById('canvas') })
    .create();

setModuleImports('main.js', {
    dom: {
        getSelectedSample: () => selectedSample,
        setResult: (message, success) => {
            const result = document.getElementById('result');
            result.innerText = message;
            result.dataset.status = success ? 'success' : 'failure';
        }
    },
    canvas: {
        getWidth: () => Math.max(1, Math.round(document.getElementById('canvas').getBoundingClientRect().width)),
        getHeight: () => Math.max(1, Math.round(document.getElementById('canvas').getBoundingClientRect().height))
    },
    mainLoop: {
        set: callback => {
            const runFrame = () => {
                callback();
                requestAnimationFrame(runFrame);
            };
            requestAnimationFrame(runFrame);
        }
    }
});

await runMain();
