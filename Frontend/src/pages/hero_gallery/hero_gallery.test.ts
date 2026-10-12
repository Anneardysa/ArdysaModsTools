import { sent } from "../../test-env";
import assert from "node:assert/strict";
import { beforeEach, describe, test } from "node:test";
import { MAX_PARTICLES, canCarryParticles, particleCarriers, sanitizeParticles, sanitizeSelection } from "./helpers";
import {
   activateFocusedSet,
   applyPresetSelections,
   openParticlePicker,
   particleSlotsLoaded,
   removeParticle,
   requestParticleSlots,
   setParticle,
   store,
   toggleItem,
} from "./store";
import type { Hero, HeroSelectionState, ParticleEntry } from "./types";

const hero: Hero = {
   id: "npc_dota_hero_juggernaut",
   name: "Juggernaut",
   thumbnail: "",
   sets: [
      { name: "Set A", category: "legacyset" },
      { name: "Set B", category: "legacyset", conflicts: [2] },
      { name: "Item X", category: "item", tag: "weapon", conflicts: [1] },
      { name: "Persona", category: "persona" },
      { name: "Base", category: "basehero" },
      { name: "Prism", category: "prismatic" },
      { name: "Default Set", category: "legacyset" },
      { name: "Helm 1", category: "item", tag: "head", styleGroup: "Helm", styleLabel: "1" },
      { name: "Helm 2", category: "item", tag: "head", styleGroup: "Helm", styleLabel: "2" },
   ],
   particleHosts: [
      { id: "6", label: "Head" },
      { id: "7", label: "Weapon" },
   ],
};

const catalog: ParticleEntry[] = [
   { id: "833", name: "Gold Drop", source: "unusual" },
   { id: "837", name: "Ravenblight", source: "unusual" },
   { id: "826", name: "Frostbloom", source: "unusual" },
];

const sel = (patch: Partial<HeroSelectionState>): HeroSelectionState => ({
   set: null, items: [], base: null, prismatic: null, effects: [], particles: [], ...patch,
});

function reset(selection?: HeroSelectionState) {
   sent.length = 0;
   store.set({
      heroes: [hero],
      particles: catalog,
      selections: selection ? { [hero.id]: selection } : {},
      status: "",
      modalHeroId: hero.id,
      focusedSetIndex: 0,
      visualOrder: [],
      stylePreview: null,
      particlePicker: null,
   });
}

const picks = () => store.get().selections[hero.id]?.particles ?? [];

describe("particle rules (helpers)", () => {
   test("the page and C# agree on the cap", () => assert.equal(MAX_PARTICLES, 2));

   test("a particle needs a real layer, never a Persona or Default Set alone", () => {
      assert.equal(canCarryParticles(hero, sel({})), false);
      assert.equal(canCarryParticles(hero, sel({ set: 6 })), false);
      assert.equal(canCarryParticles(hero, sel({ set: 3 })), false);
      assert.equal(canCarryParticles(hero, sel({ set: 0 })), true);
      assert.equal(canCarryParticles(hero, sel({ items: [2] })), true);
      assert.equal(canCarryParticles(hero, sel({ base: 4 })), true);
   });

   test("only a set, item or base carries one: their index has the block it goes into", () => {
      assert.equal(canCarryParticles(hero, sel({ base: 4, prismatic: 5 })), true);
      assert.equal(canCarryParticles(hero, sel({ prismatic: 5 })), false);
      assert.deepEqual(particleCarriers(hero, sel({ set: 0, items: [2, 7], base: 4 })), [
         { idx: 0, kind: "set" }, { idx: 2, kind: "item" }, { idx: 7, kind: "item" }, { idx: 4, kind: "base" },
      ]);
   });

   test("sanitize keeps the first two valid picks in order and names the rest", () => {
      const r = sanitizeParticles(hero, sel({
         set: 0,
         particles: [
            { id: "nope", host: "7" },
            { id: "833", host: "999" },
            { id: "837", host: " 6 " },
            { id: "837", host: "6" },
            { id: "833", host: "7" },
            { id: "826", host: "7" },
         ],
      }), catalog);
      assert.deepEqual(r.particles, [{ id: "837", host: "6" }, { id: "833", host: "7" }]);
      assert.equal(r.dropped.length, 4);
   });

   test("sanitize drops every particle when nothing carries them", () => {
      const r = sanitizeParticles(hero, sel({ particles: [{ id: "833", host: "7" }] }), catalog);
      assert.deepEqual(r.particles, []);
      assert.deepEqual(r.dropped, ["Gold Drop"]);
   });

   test("a restored Persona clears particles too", () => {
      const r = sanitizeSelection(hero, sel({ set: 3, particles: [{ id: "833", host: "7" }] }), catalog);
      assert.deepEqual(r.sel.particles, []);
      assert.ok(r.dropped.includes("Gold Drop"));
   });

   test("a Prismatic without its Base is dropped, as BuildPlan does (B2)", () => {
      const r = sanitizeSelection(hero, sel({ prismatic: 5 }), catalog);
      assert.equal(r.sel.prismatic, null);
      assert.deepEqual(r.dropped, ["Prism"]);
   });
});

describe("particle actions (store)", () => {
   beforeEach(() => reset());

   test("refused without a set, item or arcana", () => {
      assert.equal(setParticle(hero, 0, "833", "7"), false);
      assert.match(store.get().status, /Pick a set, item or arcana first/);
      assert.deepEqual(picks(), []);
   });

   test("refused under a Persona", () => {
      reset(sel({ set: 3 }));
      assert.equal(setParticle(hero, 0, "833", "7"), false);
      assert.match(store.get().status, /Persona/);
   });

   test("adds up to two, refuses a third, a duplicate, and unknown ids or hosts", () => {
      reset(sel({ set: 0 }));
      assert.equal(setParticle(hero, 0, "833", "7"), true);
      assert.equal(setParticle(hero, 1, "833", "7"), false, "same particle on the same slot");
      assert.equal(setParticle(hero, 1, "nope", "6"), false);
      assert.equal(setParticle(hero, 1, "837", "811"), false);
      assert.equal(setParticle(hero, 1, "837", "6"), true);
      assert.equal(setParticle(hero, 2, "826", "6"), false);
      assert.match(store.get().status, /at most 2/);
      assert.deepEqual(picks(), [{ id: "833", host: "7" }, { id: "837", host: "6" }]);
      assert.equal(sent[sent.length - 1]?.type, "selectionChanged", "C# hears every change");
   });

   test("a row can change effect and slot, and be removed", () => {
      reset(sel({ set: 0, particles: [{ id: "833", host: "7" }, { id: "837", host: "6" }] }));
      assert.equal(setParticle(hero, 0, "826", "7"), true);
      assert.equal(setParticle(hero, 1, "837", "7"), true);
      assert.deepEqual(picks(), [{ id: "826", host: "7" }, { id: "837", host: "7" }]);
      removeParticle(hero, 0);
      assert.deepEqual(picks(), [{ id: "837", host: "7" }]);
   });

   test("dropping the last layer takes the particles with it", () => {
      reset(sel({ items: [2], particles: [{ id: "833", host: "7" }] }));
      toggleItem(hero, 2);
      assert.equal(store.get().selections[hero.id], undefined);
   });

   test("an asset's slots are asked for once, and a failed read is asked again", () => {
      store.set({ particleSlots: {} });
      requestParticleSlots(hero, 0);
      requestParticleSlots(hero, 0);
      assert.deepEqual(sent.filter((m) => m.type === "particleSlots").length, 1);
      assert.equal(store.get().particleSlots[`${hero.id}:0`], "loading");
      particleSlotsLoaded(hero.id, 0, null);
      assert.equal(store.get().particleSlots[`${hero.id}:0`], "failed");
      requestParticleSlots(hero, 0);
      assert.deepEqual(sent.filter((m) => m.type === "particleSlots").length, 2);
      const slots = { slots: [{ id: "7", label: "Weapon", hasModel: true }], preferred: "7" };
      particleSlotsLoaded(hero.id, 0, slots);
      assert.deepEqual(store.get().particleSlots[`${hero.id}:0`], slots);
   });

   test("the picker does not open where a pick could not be made", () => {
      openParticlePicker(hero, 0);
      assert.equal(store.get().particlePicker, null);
      reset(sel({ set: 0 }));
      openParticlePicker(hero, 1);
      assert.equal(store.get().particlePicker, null, "row 1 before row 0");
      openParticlePicker(hero, 0);
      assert.deepEqual(store.get().particlePicker, { heroId: hero.id, slot: 0 });
   });
});

describe("gallery fixes", () => {
   beforeEach(() => reset());

   test("a preset's dropped picks stay on the status line (B1)", () => {
      applyPresetSelections({ [hero.id]: sel({ set: 1, items: [2] }) });
      assert.match(store.get().status, /^Dropped 1 incompatible pick/);
   });

   test("Enter on a refused set keeps the modal open (B3)", () => {
      reset(sel({ items: [2] }));
      store.set({ focusedSetIndex: 1, visualOrder: [{ idx: 1, tileType: "set" }] });
      activateFocusedSet(hero);
      assert.equal(store.get().modalHeroId, hero.id);
      assert.match(store.get().status, /cannot be combined/);
   });

   test("Enter on an allowed set picks it and closes the modal", () => {
      store.set({ focusedSetIndex: 0, visualOrder: [{ idx: 0, tileType: "set" }] });
      activateFocusedSet(hero);
      assert.equal(store.get().modalHeroId, null);
   });

   test("Enter on a style group opens its variants instead of taking the first", () => {
      store.set({ focusedSetIndex: 7, visualOrder: [{ idx: 7, tileType: "item" }] });
      activateFocusedSet(hero);
      assert.equal(store.get().stylePreview?.group, "Helm");
      assert.equal(store.get().selections[hero.id], undefined, "nothing picked yet");
   });
});
