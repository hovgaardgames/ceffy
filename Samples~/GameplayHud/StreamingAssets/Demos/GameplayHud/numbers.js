"use strict";

(() => {
    const duration = 350;
    const counters = [];
    const motion = window.matchMedia("(prefers-reduced-motion: reduce)");
    let frame = 0;

    class Counter {
        constructor(render) {
            this.render = render;
            this.value = 0;
            this.from = 0;
            this.target = 0;
            this.startedAt = 0;
            this.initialized = false;
            this.running = false;
        }

        set(value, immediate = false) {
            if (!Number.isFinite(value)) return;
            const now = performance.now();
            const shouldSnap = immediate || !this.initialized || motion.matches || document.hidden;
            this.initialized = true;
            if (shouldSnap) {
                this.target = value;
                this.finish();
                return;
            }
            if (value === this.target) {
                this.render(this.value);
                return;
            }

            // Retarget from the interpolated value, not the last snapshot, when pickups arrive in a burst.
            this.advance(now);
            this.from = this.value;
            this.target = value;
            this.startedAt = now;
            this.running = true;
            scheduleFrame();
        }

        advance(now) {
            if (!this.running) return;
            const progress = Math.min(1, (now - this.startedAt) / duration);
            const eased = 1 - Math.pow(1 - progress, 3);
            this.value = this.from + (this.target - this.from) * eased;
            if (progress >= 1) {
                this.finish();
                return;
            }
            this.render(this.value);
        }

        finish() {
            if (!this.initialized) return;
            this.value = this.target;
            this.from = this.target;
            this.running = false;
            this.render(this.value);
        }
    }

    function scheduleFrame() {
        if (!frame) frame = requestAnimationFrame(animate);
    }

    function animate(now) {
        frame = 0;
        let running = false;
        for (const counter of counters) {
            counter.advance(now);
            running = running || counter.running;
        }
        if (running) scheduleFrame();
    }

    function finishAnimations() {
        cancelAnimationFrame(frame);
        frame = 0;
        for (const counter of counters) counter.finish();
    }

    document.addEventListener("visibilitychange", () => {
        if (document.hidden) finishAnimations();
    });
    motion.addEventListener("change", () => {
        if (motion.matches) finishAnimations();
    });

    window.OutpostNumbers = {
        motion,
        createCounter(render) {
            const counter = new Counter(render);
            counters.push(counter);
            return counter;
        }
    };
})();
