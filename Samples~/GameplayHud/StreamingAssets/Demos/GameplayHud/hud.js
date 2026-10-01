"use strict";

(() => {
    const elements = Object.fromEntries([...document.querySelectorAll("[id]")].map(el => [el.id, el]));
    const appearanceControls = elements.appearance.querySelectorAll("button, input");
    let state = null;
    let connected = false;
    let hasStarted = false;
    let toastTimer;
    let hitFrame;
    let lastRegions = "";
    let commandPending = false;
    let currentTheme = "green";
    let numericMatchId = null;
    let collectedEnergy = 0;
    let gainTotal = 0;
    let gainAnimation = null;
    let energyAnimation = null;
    let gainTimer;
    const numberFormat = new Intl.NumberFormat();
    const counters = {
        health: window.OutpostNumbers.createCounter(value => {
            setText(elements.health, `${Math.round(value)} / ${state.maxHealth}`);
            elements["health-bar"].value = value;
        }),
        generator: window.OutpostNumbers.createCounter(value => {
            setText(elements.generator, `${Math.round(value)} / ${state.maxGeneratorHealth}`);
            elements["generator-bar"].value = value;
        }),
        energy: window.OutpostNumbers.createCounter(value => setText(elements.energy, numberFormat.format(Math.round(value)))),
        score: window.OutpostNumbers.createCounter(value => setText(elements.score, numberFormat.format(Math.round(value))))
    };

    function setText(element, text) {
        if (element.textContent !== text) element.textContent = text;
    }

    function updateNumbers() {
        const isNewMatch = numericMatchId !== state.matchId;
        if (isNewMatch) resetPickupFeedback();
        elements["health-bar"].max = state.maxHealth;
        elements["generator-bar"].max = state.maxGeneratorHealth;
        counters.health.set(state.health, isNewMatch);
        counters.generator.set(state.generatorHealth, isNewMatch);
        counters.energy.set(state.energy, isNewMatch);
        counters.score.set(state.score, isNewMatch);
        const pickupAmount = state.collectedEnergy - collectedEnergy;
        if (!isNewMatch && pickupAmount > 0) showPickupFeedback(pickupAmount);
        numericMatchId = state.matchId;
        collectedEnergy = state.collectedEnergy;
    }

    function resetPickupFeedback() {
        clearTimeout(gainTimer);
        gainAnimation?.cancel();
        energyAnimation?.cancel();
        gainAnimation = null;
        energyAnimation = null;
        gainTotal = 0;
        elements["energy-gain"].hidden = true;
    }

    function showPickupFeedback(amount) {
        if (document.hidden) return;
        gainTotal += amount;
        const badge = elements["energy-gain"];
        badge.textContent = `+${numberFormat.format(gainTotal)} E`;
        badge.hidden = false;
        clearTimeout(gainTimer);
        gainAnimation?.cancel();
        energyAnimation?.cancel();
        if (window.OutpostNumbers.motion.matches) {
            gainTimer = setTimeout(resetPickupFeedback, 900);
            return;
        }
        const animation = badge.animate([
            { opacity: 0, transform: "translateY(8px)" },
            { opacity: 1, transform: "translateY(-8px)", offset: 0.2 },
            { opacity: 1, transform: "translateY(-18px)", offset: 0.65 },
            { opacity: 0, transform: "translateY(-30px)" }
        ], { duration: 900, easing: "ease-out" });
        gainAnimation = animation;
        animation.onfinish = () => {
            if (gainAnimation === animation) resetPickupFeedback();
        };
        energyAnimation = elements.energy.animate([
            { transform: "scale(1)", filter: "brightness(1)" },
            { transform: "scale(1.15)", filter: "brightness(1.5)", offset: 0.3 },
            { transform: "scale(1)", filter: "brightness(1)" }
        ], { duration: 450, easing: "ease-out" });
    }

    document.addEventListener("visibilitychange", () => {
        if (document.hidden) resetPickupFeedback();
    });
    window.OutpostNumbers.motion.addEventListener("change", () => {
        if (window.OutpostNumbers.motion.matches) resetPickupFeedback();
    });

    function notify(message) {
        elements.toast.textContent = message;
        elements.toast.hidden = false;
        clearTimeout(toastTimer);
        toastTimer = setTimeout(() => { elements.toast.hidden = true; }, 4000);
    }

    function updateState(next) {
        state = next;
        updateNumbers();
        elements.wave.textContent = state.wave ? `Wave ${String(state.wave).padStart(2, "0")}` : "Prepare your defenses";
        elements.phase.textContent = state.isWaveActive
            ? `${state.enemies} raiders remaining`
            : `Next wave in ${state.countdown}s · ${state.turrets}/3 turrets`;
        elements["build-cost"].textContent = `${state.buildCost} E`;
        elements["repair-cost"].textContent = `${state.repairCost} E`;
        elements["heal-cost"].textContent = `${state.healCost} E`;
        const unavailable = state.isPaused || state.isDefeated || commandPending;
        elements.repair.disabled = unavailable || state.energy < state.repairCost
            || state.generatorHealth >= state.maxGeneratorHealth;
        elements.heal.disabled = unavailable || state.energy < state.healCost || state.health >= state.maxHealth;
        elements["next-wave"].disabled = unavailable || state.isWaveActive;
        elements.pause.disabled = commandPending || state.isDefeated;
        elements.dialog.hidden = !state.isPaused && !state.isDefeated;
        updateAppearancePlacement();
        updateAppearanceControls();
        elements.resume.hidden = state.isDefeated;
        elements.resume.disabled = commandPending;
        elements.resume.textContent = hasStarted ? "Resume defense →" : "Begin defense →";
        elements.restart.hidden = !hasStarted && !state.isDefeated;
        elements.restart.disabled = commandPending;
        elements["dialog-title"].textContent = state.isDefeated ? "Outpost lost." : hasStarted ? "Take a breath." : "Hold the line.";
        elements["dialog-message"].textContent = state.isDefeated
            ? `You reached wave ${state.wave} with ${state.score.toLocaleString()} points. Try a different defense.`
            : "Protect the generator. Every raider drops energy to fund your defenses.";
        if (document.activeElement !== elements["hud-zoom"]) {
            elements["hud-zoom"].value = state.hudZoomPercent;
            elements["zoom-value"].textContent = `${Math.round(state.hudZoomPercent)}%`;
        }
        applyTheme(state.theme);
        scheduleHitRegions();
    }

    function updateAppearancePlacement() {
        const parent = elements.dialog.hidden ? document.body : elements["dialog-content"];
        if (elements.appearance.parentElement !== parent) parent.append(elements.appearance);
    }

    function updateAppearanceControls() {
        for (const control of appearanceControls) control.disabled = !connected || commandPending;
    }

    function applyTheme(theme) {
        if (!["green", "pink", "blue"].includes(theme)) return;
        if (theme !== currentTheme) {
            currentTheme = theme;
            elements["theme-style"].href = `themes/${theme}.css`;
        }
        document.querySelectorAll("[data-theme]").forEach(button => {
            button.setAttribute("aria-pressed", String(button.dataset.theme === theme));
        });
    }

    async function command(method, ...args) {
        if (!connected || commandPending) return;
        commandPending = true;
        if (state) updateState(state);
        try {
            await window.ceffy.unity[method](...args);
            updateState(await window.ceffy.unity.getState());
        } catch (error) {
            notify(`Command failed: ${error.message}`);
            console.error("[Outpost]", error);
        } finally {
            commandPending = false;
            if (state) updateState(state);
        }
    }

    function scheduleHitRegions() {
        if (hitFrame) return;
        hitFrame = requestAnimationFrame(() => {
            hitFrame = 0;
            if (!connected) return;
            const toolbarBottom = elements.status.getBoundingClientRect().bottom + 16;
            document.documentElement.style.setProperty("--toolbar-bottom", `${toolbarBottom}px`);
            // Alpha is visual only: Unity's RawImage needs explicit DOM rectangles for click-through.
            const regions = [...document.querySelectorAll("[data-hit]")]
                .filter(el => el.getClientRects().length > 0)
                .map(el => {
                    const rect = el.getBoundingClientRect();
                    return {
                        x: rect.x / innerWidth, y: rect.y / innerHeight,
                        width: rect.width / innerWidth, height: rect.height / innerHeight
                    };
                });
            const signature = JSON.stringify(regions);
            if (signature === lastRegions) return;
            lastRegions = signature;
            window.ceffy.unity.setInputRegions(regions).catch(error => {
                lastRegions = "";
                console.error("[Outpost] Hit regions:", error);
            });
        });
    }

    function registerUi() {
        window.ceffy.ui.updateState = updateState;
        window.ceffy.ui.showNotification = notify;
    }

    document.addEventListener("ceffy:ready", registerUi);
    if (window.ceffy?._bridgeReady) registerUi();

    // Bind<T> injects command stubs after the core ready event. Wait for those stubs too.
    const handshake = setInterval(async () => {
        if (!window.ceffy?.unity?.getState) {
            window.ceffy?.SendToUnity?.("outpost:ready");
            return;
        }
        clearInterval(handshake);
        registerUi();
        connected = true;
        elements.connection.textContent = "Connected · HUD RPC + shared panel messaging";
        elements.startup.textContent = "Local HTML / CSS / JavaScript. No network connection required.";
        try {
            updateState(await window.ceffy.unity.getState());
        } catch (error) {
            notify(`Connection failed: ${error.message}`);
        }
        scheduleHitRegions();
    }, 150);

    setTimeout(() => {
        if (!connected) elements.startup.textContent =
            "Still connecting. Check Unity's Console and Assets/StreamingAssets/Demos/GameplayHud files.";
    }, 12000);

    elements["hud-zoom"].addEventListener("input", event => {
        elements["zoom-value"].textContent = `${event.target.value}%`;
    });
    // Apply on release so native browser zoom does not move the slider while it is being dragged.
    elements["hud-zoom"].addEventListener("change", event => {
        command("setHudZoomPercent", Number(event.target.value));
        event.target.blur();
    });
    elements["zoom-reset"].addEventListener("click", () => command("setHudZoomPercent", 150));
    document.querySelectorAll("[data-theme]").forEach(button => {
        button.addEventListener("click", () => command("setTheme", button.dataset.theme));
    });
    elements.repair.addEventListener("click", () => command("repairGenerator"));
    elements.heal.addEventListener("click", () => command("healPlayer"));
    elements["next-wave"].addEventListener("click", () => command("startWave"));
    elements.pause.addEventListener("click", () => command("setPaused", true));
    elements.resume.addEventListener("click", () => { hasStarted = true; command("setPaused", false); });
    elements.restart.addEventListener("click", () => { hasStarted = true; command("restart"); });
    window.addEventListener("resize", scheduleHitRegions);
    elements["theme-style"].addEventListener("load", scheduleHitRegions);
    new ResizeObserver(scheduleHitRegions).observe(elements.operations);
    new ResizeObserver(scheduleHitRegions).observe(elements.appearance);
    new ResizeObserver(scheduleHitRegions).observe(elements.status);
    updateAppearancePlacement();
    updateAppearanceControls();
})();
