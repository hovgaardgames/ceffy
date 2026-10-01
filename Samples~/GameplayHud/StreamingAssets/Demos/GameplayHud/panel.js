"use strict";

(() => {
    const panel = document.getElementById("panel");
    const title = document.getElementById("title");
    const value = document.getElementById("value");
    const health = document.getElementById("health");
    const action = document.getElementById("action");
    let kind = "enemy";
    let command = "";
    let theme = "green";
    let readyTimer;

    function receive(raw) {
        let state;
        try {
            state = typeof raw === "string" ? JSON.parse(raw) : raw;
        } catch (error) {
            console.error("[Outpost panel] Invalid state:", error);
            return;
        }
        clearInterval(readyTimer);
        kind = state.kind;
        command = state.command;
        if (["green", "pink", "blue"].includes(state.theme) && state.theme !== theme) {
            theme = state.theme;
            document.getElementById("theme-style").href = `themes/${theme}.css`;
        }
        document.body.className = kind;
        panel.hidden = !state.isVisible;
        title.textContent = state.title;
        value.textContent = `${state.health}/${state.maxHealth}`;
        health.max = state.maxHealth || 1;
        health.value = state.health;
        action.hidden = kind === "enemy";
        action.disabled = !state.canAct;
        action.textContent = state.action;
    }

    // Shared iframes expose the same transport as dedicated pages, scoped to their owning slot.
    function connect() {
        if (!window.ceffy?.SendToUnity) return;
        window.ceffy.onMessageFromUnity = receive;
        window.ceffy.SendToUnity("panel:ready");
    }

    readyTimer = setInterval(connect, 200);
    connect();
    action.addEventListener("click", () => {
        if (action.disabled) return;
        action.disabled = true;
        window.ceffy.SendToUnity(command);
    });
})();
