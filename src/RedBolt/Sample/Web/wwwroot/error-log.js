// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

(function () {
    'use strict';

    const entries = [];
    let overlay;
    let logElement;
    let toggleButton;
    let renderedEntryCount = 0;

    function formatValue(value) {
        if (typeof value === 'string') {
            return value;
        }
        if (value instanceof Error) {
            return value.stack || value.message || String(value);
        }
        if (value === undefined) {
            return 'undefined';
        }
        try {
            const json = JSON.stringify(value);
            return json === undefined ? String(value) : json;
        } catch (_) {
            return String(value);
        }
    }

    function formatEntry(entry) {
        return `${entry.timestamp} [${entry.level}] ${entry.message}`;
    }

    function renderPendingEntries() {
        if (!logElement) {
            return;
        }
        while (renderedEntryCount < entries.length) {
            const entry = entries[renderedEntryCount++];
            logElement.append(document.createTextNode(`${formatEntry(entry)}\n`));
        }
        logElement.scrollTop = logElement.scrollHeight;
    }

    function show() {
        if (!overlay) {
            return;
        }
        overlay.hidden = false;
        overlay.classList.remove('collapsed');
        if (toggleButton) {
            toggleButton.innerText = 'Hide';
        }
        renderPendingEntries();
    }

    function addEntry(level, values) {
        entries.push({
            timestamp: new Date().toISOString(),
            level,
            message: Array.prototype.map.call(values, formatValue).join(' ')
        });
        if (overlay && !overlay.hidden && !overlay.classList.contains('collapsed')) {
            renderPendingEntries();
        }
        if (level === 'error') {
            show();
        }
    }

    function textForEntries(errorsOnly) {
        return entries
            .filter(entry => !errorsOnly || entry.level === 'error')
            .map(formatEntry)
            .join('\n');
    }

    function copyText(text) {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            return navigator.clipboard.writeText(text);
        }

        const textArea = document.createElement('textarea');
        textArea.value = text;
        textArea.setAttribute('readonly', '');
        textArea.style.position = 'fixed';
        textArea.style.opacity = '0';
        document.body.append(textArea);
        textArea.select();
        try {
            document.execCommand('copy');
        } finally {
            textArea.remove();
        }
        return Promise.resolve();
    }

    function initializeOverlay() {
        overlay = document.getElementById('diagnostics');
        logElement = document.getElementById('diagnostics-log');
        toggleButton = document.getElementById('diagnostics-toggle');
        const copyAllButton = document.getElementById('diagnostics-copy-all');
        const copyErrorsButton = document.getElementById('diagnostics-copy-errors');

        if (!overlay || !logElement || !toggleButton || !copyAllButton || !copyErrorsButton) {
            overlay = undefined;
            logElement = undefined;
            toggleButton = undefined;
            return;
        }

        toggleButton.addEventListener('click', () => {
            const collapsed = overlay.classList.toggle('collapsed');
            toggleButton.innerText = collapsed ? 'Show' : 'Hide';
            if (!collapsed) {
                renderPendingEntries();
            }
        });
        copyAllButton.addEventListener('click', () => {
            copyText(textForEntries(false)).catch(error => addEntry('error', [error]));
        });
        copyErrorsButton.addEventListener('click', () => {
            copyText(textForEntries(true)).catch(error => addEntry('error', [error]));
        });

        if (entries.some(entry => entry.level === 'error')) {
            show();
        }
    }

    for (const level of ['debug', 'log', 'info', 'warn', 'error']) {
        const original = console[level];
        console[level] = function () {
            addEntry(level === 'error' ? 'error' : level, arguments);
            return original.apply(console, arguments);
        };
    }

    window.addEventListener('error', event => {
        if (event.error || event.message) {
            const location = event.filename
                ? ` (${event.filename}:${event.lineno || 0}:${event.colno || 0})`
                : '';
            addEntry('error', [`${event.message || 'Uncaught error'}${location}`, event.error || '']);
            return;
        }

        const target = event.target;
        if (target && target !== window) {
            addEntry('error', [`Failed to load ${target.src || target.href || target.tagName || 'resource'}`]);
        }
    }, true);
    window.addEventListener('unhandledrejection', event => {
        addEntry('error', ['Unhandled promise rejection:', event.reason]);
    });

    window.redBoltDiagnostics = {
        log: function (level) {
            addEntry(level === 'error' ? 'error' : level, Array.prototype.slice.call(arguments, 1));
        },
        show
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeOverlay, { once: true });
    } else {
        initializeOverlay();
    }
}());
