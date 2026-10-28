(function () {
    let tooltip;

    function ensureTooltip() {
        if (!tooltip) {
            tooltip = document.createElement('div');
            tooltip.className = 'chart-tooltip';
            tooltip.hidden = true;
            document.body.appendChild(tooltip);
        }
        return tooltip;
    }

    function showTooltip(x, y, label, value) {
        const el = ensureTooltip();
        el.textContent = '';
        const strong = document.createElement('strong');
        strong.textContent = value;
        const span = document.createElement('span');
        span.textContent = label;
        el.appendChild(strong);
        el.appendChild(span);
        el.style.left = `${x + 14}px`;
        el.style.top = `${y + 14}px`;
        el.hidden = false;
    }

    function hideTooltip() {
        if (tooltip) {
            tooltip.hidden = true;
        }
    }

    function initHitTargets(root) {
        root.querySelectorAll('.chart-hit').forEach((el) => {
            el.addEventListener('pointermove', (e) => {
                showTooltip(e.clientX, e.clientY, el.dataset.label, el.dataset.value);
                el.classList.add('is-hover');
            });
            el.addEventListener('pointerleave', () => {
                hideTooltip();
                el.classList.remove('is-hover');
            });
            el.addEventListener('focus', () => {
                const rect = el.getBoundingClientRect();
                showTooltip(rect.left, rect.bottom, el.dataset.label, el.dataset.value);
            });
            el.addEventListener('blur', hideTooltip);
        });
    }

    function initLinePlot(svg) {
        const crosshair = svg.querySelector('.chart-crosshair');
        const points = Array.from(svg.querySelectorAll('.chart-point-hit'));
        if (!points.length) {
            return;
        }

        svg.addEventListener('pointermove', (e) => {
            const rect = svg.getBoundingClientRect();
            const viewBox = svg.viewBox.baseVal;
            const scaleX = viewBox.width / rect.width;
            const localX = (e.clientX - rect.left) * scaleX;

            let nearest = points[0];
            let best = Infinity;
            for (const p of points) {
                const d = Math.abs(parseFloat(p.dataset.x) - localX);
                if (d < best) {
                    best = d;
                    nearest = p;
                }
            }

            const px = parseFloat(nearest.dataset.x);
            if (crosshair) {
                crosshair.setAttribute('x1', px);
                crosshair.setAttribute('x2', px);
                crosshair.removeAttribute('hidden');
            }
            showTooltip(e.clientX, e.clientY, nearest.dataset.label, nearest.dataset.value);
        });

        svg.addEventListener('pointerleave', () => {
            hideTooltip();
            if (crosshair) {
                crosshair.setAttribute('hidden', '');
            }
        });
    }

    window.initDashboardCharts = function () {
        document.querySelectorAll('[data-chart="bar"], [data-chart="stacked"]').forEach(initHitTargets);
        document.querySelectorAll('[data-chart="line"]').forEach((svg) => {
            initHitTargets(svg);
            initLinePlot(svg);
        });
    };
})();
