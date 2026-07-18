import { dotnet } from './_framework/dotnet.js'

const vfs = await fetch('./resources.manifest.json').then(response => response.json());
const { setModuleImports, runMain } = await dotnet
    .withConfig({ resources: { vfs } })
    .withModuleConfig({ canvas: document.getElementById('canvas') })
    .create();

setModuleImports('main.js', {
    dom: {
        setResult: (message, success) => {
            const result = document.getElementById('result');
            result.innerText = message;
            result.dataset.status = success ? 'success' : 'failure';
        }
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
