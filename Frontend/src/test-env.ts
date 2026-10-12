export const sent: { type: string; [k: string]: unknown }[] = [];

const events = new EventTarget();
Object.assign(globalThis, {
   window: globalThis,
   addEventListener: events.addEventListener.bind(events),
   removeEventListener: events.removeEventListener.bind(events),
   dispatchEvent: events.dispatchEvent.bind(events),
   chrome: { webview: { postMessage: (m: unknown) => void sent.push(m as (typeof sent)[number]) } },
});
