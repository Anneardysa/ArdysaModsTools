import { sent } from "../../test-env";
import assert from "node:assert/strict";
import { beforeEach, describe, test } from "node:test";
import { iconForOption } from "./dotaIcons";
import { filterChoices, isCustomized, navGroups, shownStyleId, stepStyle } from "./helpers";
import {
   activeOption,
   clearSelections,
   closeTopLayer,
   closeTribute,
   cycleStyle,
   generate,
   optionsLoaded,
   resetOption,
   selectChoice,
   setActiveOption,
   store,
   toggleEthereal,
   type Choice,
   type MiscOption,
} from "./store";
import { getThumbUrl, isDefaultChoice } from "./thumbnails";

const opt = (id: string, category: string, choices: string[], extra: Partial<MiscOption> = {}): MiscOption => ({
   id,
   name: id,
   category,
   thumbnailPattern: `https://cdn/${id}/{choice}.webp`,
   choices: choices.map((c) => ({ id: c, name: c })),
   ...extra,
});

const bearzky: Choice = {
   id: "Bearzky",
   name: "Bearzky",
   styles: [
      { id: "Bearzky (Style 0)", name: "Bearzky (Style 0)" },
      { id: "Bearzky (Style 1)", name: "Bearzky (Style 1)" },
   ],
};

const options: MiscOption[] = [
   opt("ancient", "Environment", ["Default Ancient", "Crownfall Ancient"]),
   opt("Special", "Environment", ["Disable Special", "LowPolyMap"], { excludesWith: ["Map"] }),
   opt("Map", "Environment", ["Default Terrain", "Desert Terrain"], { excludesWith: ["Special"] }),
   opt("Music", "Audio & Visual", ["Default Music", "Heroic Music"]),
   opt("HUD", "Interface", ["Default HUD", "Ti HUD"]),
   opt("Courier", "Gameplay", ["Default Courier", "Ageless Apothecary"]),
   opt("Ward", "Gameplay", ["Default Ward", "Frosty the North Ward"]),
];
options[5]!.choices.push(bearzky);

function reset(patch: Partial<ReturnType<typeof store.get>> = {}) {
   sent.length = 0;
   store.set({
      options,
      selections: {},
      activeOptionId: null,
      search: "",
      etherealOpen: false,
      generating: false,
      status: "",
      flashResetId: null,
      modeModalOpen: false,
      logModalOpen: false,
      alert: { visible: false, title: "", message: "", type: "info", hasLog: false },
      progress: { visible: false, title: "", percent: 0, status: "", lines: [], flash: null },
      tribute: null,
      tributeShownFor: [],
      generateAfterTribute: false,
      ...patch,
   });
}

beforeEach(() => reset());

describe("sidebar order", () => {
   test("Courier and Ward lead, the rest keep config order, each category once", () => {
      assert.deepEqual(
         navGroups(options).map((g) => [g.category, g.options.map((o) => o.id)]),
         [
            ["Gameplay", ["Courier", "Ward"]],
            ["Environment", ["ancient", "Special", "Map"]],
            ["Audio & Visual", ["Music"]],
            ["Interface", ["HUD"]],
         ],
      );
   });

   test("no options, no groups; a missing category lands in Other", () => {
      assert.deepEqual(navGroups([]), []);
      assert.equal(navGroups([opt("x", "", ["a"])])[0]!.category, "Other");
   });

   test("the pane opens on the first sidebar item until one is picked", () => {
      assert.equal(activeOption()?.id, "Courier");
      setActiveOption("HUD");
      assert.equal(activeOption()?.id, "HUD");
      store.set({ activeOptionId: "gone" });
      assert.equal(activeOption()?.id, "Courier");
   });

   test("switching option clears the search and closes the ethereal dialog", () => {
      store.set({ search: "frost", etherealOpen: true });
      setActiveOption("Ward");
      assert.equal(store.get().search, "");
      assert.equal(store.get().etherealOpen, false);
   });

   test("optionsLoaded drops the caching overlay and any earlier load error", () => {
      store.set({ cachingVisible: true, loadError: "boom" });
      optionsLoaded(options);
      assert.equal(store.get().cachingVisible, false);
      assert.equal(store.get().loadError, null);
   });
});

describe("Dota icons", () => {
   const liveIds = [
      "ancient", "Special", "River", "roshan", "tormentor", "Map", "Weather", "announcer", "atkModifier",
      "Effect", "Emblems", "kill_streak", "mega_kills", "multikill_banner", "Music", "Shader", "cursor",
      "HUD", "Versus", "RadiantCreep", "RadiantSiege", "RadiantTower", "DireCreep", "DireSiege", "DireTower",
      "Courier", "Ward",
   ];
   for (const id of liveIds) {
      test(`${id} has its own glyph`, () => assert.notEqual(iconForOption(id), "other"));
   }

   test("lookup ignores case (C# option ids are case-insensitive) and unknown ids fall back", () => {
      assert.equal(iconForOption("COURIER"), "couriers");
      assert.equal(iconForOption("brand_new_option"), "other");
   });
});

describe("selecting", () => {
   test("a choice is stored and sent to the host", () => {
      selectChoice("HUD", "Ti HUD");
      assert.equal(store.get().selections.HUD, "Ti HUD");
      assert.deepEqual(sent, [{ type: "selectionChanged", optionId: "HUD", choiceId: "Ti HUD" }]);
   });

   test("re-selecting the current choice sends nothing", () => {
      reset({ selections: { HUD: "Ti HUD" } });
      selectChoice("HUD", "Ti HUD");
      assert.deepEqual(sent, []);
   });

   test("Low Poly Map resets a custom Terrain, and the reset is sent and flashed", () => {
      reset({ selections: { Map: "Desert Terrain" } });
      selectChoice("Special", "LowPolyMap");
      assert.equal(store.get().selections.Map, "Default Terrain");
      assert.equal(store.get().flashResetId, "Map");
      assert.deepEqual(sent, [
         { type: "selectionChanged", optionId: "Special", choiceId: "LowPolyMap" },
         { type: "selectionChanged", optionId: "Map", choiceId: "Default Terrain" },
      ]);
   });

   test("going back to a default choice resets nothing it excludes", () => {
      reset({ selections: { Special: "LowPolyMap", Map: "Desert Terrain" } });
      selectChoice("Special", "Disable Special");
      assert.equal(store.get().selections.Map, "Desert Terrain");
      assert.equal(sent.length, 1);
   });

   test("Reset puts the option back on its first choice", () => {
      reset({ selections: { HUD: "Ti HUD" } });
      resetOption("HUD");
      assert.equal(store.get().selections.HUD, "Default HUD");
   });

   test("an unknown option is ignored", () => {
      selectChoice("nope", "x");
      assert.deepEqual(sent, []);
   });
});

describe("style pager", () => {
   test("shows the selected style, else the first one, and wraps both ways", () => {
      assert.equal(shownStyleId(bearzky, "Bearzky (Style 1)"), "Bearzky (Style 1)");
      assert.equal(shownStyleId(bearzky, "Default Courier"), "Bearzky (Style 0)");
      assert.equal(stepStyle(bearzky, "Bearzky (Style 1)", 1), "Bearzky (Style 0)");
      assert.equal(stepStyle(bearzky, "Bearzky (Style 0)", -1), "Bearzky (Style 1)");
   });

   test("cycling the selected card changes the selection", () => {
      reset({ selections: { Courier: "Bearzky (Style 0)" } });
      assert.equal(cycleStyle("Courier", bearzky, "Bearzky (Style 0)", 1), "Bearzky (Style 1)");
      assert.equal(store.get().selections.Courier, "Bearzky (Style 1)");
      assert.deepEqual(sent, [{ type: "selectionChanged", optionId: "Courier", choiceId: "Bearzky (Style 1)" }]);
   });

   test("cycling an unselected card only previews", () => {
      reset({ selections: { Courier: "Ageless Apothecary" } });
      assert.equal(cycleStyle("Courier", bearzky, "Bearzky (Style 0)", 1), "Bearzky (Style 1)");
      assert.equal(store.get().selections.Courier, "Ageless Apothecary");
      assert.deepEqual(sent, []);
   });
});

describe("ethereal effects", () => {
   const courier = options[5]!;
   const ward = options[6]!;

   test("up to two per option, stored under <id>Ethereal, and the third is ignored", () => {
      toggleEthereal(courier, "Dark Moon");
      toggleEthereal(courier, "Ionic Vapor");
      toggleEthereal(courier, "Desert Sands");
      assert.equal(store.get().selections.CourierEthereal, "Dark Moon,Ionic Vapor");
      assert.equal(sent.length, 2);
      assert.deepEqual(sent[1], { type: "selectionChanged", optionId: "CourierEthereal", choiceId: "Dark Moon,Ionic Vapor" });
   });

   test("toggling off frees the slot; Ward keeps its own key", () => {
      reset({ selections: { CourierEthereal: "Dark Moon,Ionic Vapor" } });
      toggleEthereal(courier, "Dark Moon");
      toggleEthereal(ward, "Dark Moon");
      assert.equal(store.get().selections.CourierEthereal, "Ionic Vapor");
      assert.equal(store.get().selections.WardEthereal, "Dark Moon");
   });
});

describe("customized marker", () => {
   test("counts a non-default pick, and ethereal effects on the default courier, but not default picks", () => {
      const [, , , , hud, courier, ward] = options;
      assert.equal(isCustomized(hud!, { HUD: "Default HUD" }), false);
      assert.equal(isCustomized(hud!, { HUD: "Ti HUD" }), true);
      assert.equal(isCustomized(courier!, { CourierEthereal: "Dark Moon" }), true);
      assert.equal(isCustomized(ward!, { CourierEthereal: "Dark Moon" }), false);
      assert.equal(isCustomized(hud!, { HUDEthereal: "Dark Moon" }), false);
   });
});

describe("Esc closes the topmost layer only", () => {
   test("a dialog over the ethereal list closes alone, without closing the window", () => {
      reset({ etherealOpen: true, logModalOpen: true });
      closeTopLayer();
      assert.equal(store.get().logModalOpen, false);
      assert.equal(store.get().etherealOpen, true);
      closeTopLayer();
      assert.equal(store.get().etherealOpen, false);
      assert.deepEqual(sent, []);
   });

   test("the mode prompt is answered with no mode, so the host stops waiting, and the window stays", () => {
      reset({ modeModalOpen: true });
      closeTopLayer();
      assert.equal(store.get().modeModalOpen, false);
      assert.deepEqual(sent, [{ type: "modeSelected", mode: null }]);
   });

   test("an alert is dismissed before anything else", () => {
      reset({ alert: { visible: true, title: "t", message: "m", type: "error", hasLog: false }, etherealOpen: true });
      closeTopLayer();
      assert.deepEqual(sent, [{ type: "alertDismissed" }]);
      assert.equal(store.get().etherealOpen, true);
   });

   test("the tribute ignores Esc: its close waits out a countdown", () => {
      reset({ tribute: { choiceName: "Big Vpk" }, generateAfterTribute: true });
      closeTopLayer();
      assert.deepEqual(store.get().tribute, { choiceName: "Big Vpk" });
      assert.deepEqual(sent, []);
   });

   test("a running generation ignores Esc", () => {
      reset({ progress: { visible: true, title: "", percent: 10, status: "", lines: [], flash: null }, generating: true });
      closeTopLayer();
      assert.deepEqual(sent, []);
   });

   test("a search is cleared before the window closes", () => {
      reset({ search: "frost" });
      closeTopLayer();
      assert.equal(store.get().search, "");
      assert.deepEqual(sent, []);
      closeTopLayer();
      assert.deepEqual(sent, [{ type: "close" }]);
   });

   test("nothing open while generating: no close", () => {
      reset({ generating: true });
      closeTopLayer();
      assert.deepEqual(sent, []);
   });
});

describe("header actions", () => {
   test("Clear reports whether anything was actually set", () => {
      reset({ selections: { HUD: "Default HUD" } });
      clearSelections();
      assert.equal(store.get().status, "Already at default");
      reset({ selections: { CourierEthereal: "Dark Moon" } });
      clearSelections();
      assert.equal(store.get().status, "Selections cleared");
      assert.deepEqual(store.get().selections, {});
      assert.deepEqual(sent, [{ type: "clearSelections" }]);
   });

   test("Clear is ignored while generating", () => {
      reset({ generating: true, selections: { HUD: "Ti HUD" } });
      clearSelections();
      assert.equal(store.get().selections.HUD, "Ti HUD");
      assert.deepEqual(sent, []);
   });

   test("Generate sends the selections", () => {
      reset({ selections: { HUD: "Ti HUD" } });
      generate();
      assert.deepEqual(sent, [{ type: "generate", selections: { HUD: "Ti HUD" } }]);
   });

   test("a special-VPK pick shows the tribute first and generates exactly once when it closes", () => {
      const special = opt("Vpk", "Gameplay", ["Default Vpk", "Big Vpk"], { isSpecialVpk: true });
      reset({ options: [...options, special], selections: { Vpk: "Big Vpk" } });
      generate();
      assert.deepEqual(store.get().tribute, { choiceName: "Big Vpk" });
      assert.deepEqual(sent, []);
      closeTribute();
      closeTribute();
      assert.deepEqual(sent, [{ type: "generate", selections: { Vpk: "Big Vpk" } }]);
   });

   test("picking a special-VPK choice shows the tribute once per session", () => {
      const special = opt("Vpk", "Gameplay", ["Default Vpk", "Big Vpk", "Small Vpk"], { isSpecialVpk: true });
      reset({ options: [...options, special] });
      selectChoice("Vpk", "Big Vpk");
      assert.ok(store.get().tribute);
      closeTribute();
      selectChoice("Vpk", "Small Vpk");
      assert.equal(store.get().tribute, null);
   });
});

describe("search", () => {
   const choices = options[5]!.choices;
   test("matches choice and style names, ignoring case and outer spaces", () => {
      assert.deepEqual(filterChoices(choices, "  AGELESS ").map((c) => c.id), ["Ageless Apothecary"]);
      assert.deepEqual(filterChoices(choices, "style 1").map((c) => c.id), ["Bearzky"]);
      assert.equal(filterChoices(choices, "").length, choices.length);
      assert.deepEqual(filterChoices(choices, "zzz"), []);
   });
});

describe("thumbnails mirror MiscOption.GetThumbnailUrl (Tests/Models/MiscOptionTests.cs)", () => {
   const o = (extra: Partial<MiscOption> = {}) => opt("Courier", "Gameplay", [], { thumbnailPattern: "https://cdn/{choice}.webp", ...extra });
   const cases: [string, string][] = [
      ["10th Anniversary Filmtail", "10th_anniversary_filmtail"],
      ["Collector's Baby Roshan 2017", "collectors_baby_roshan_2017"],
      ["Corsair, Son of the Storm", "corsair_son_of_the_storm"],
      ["Fezzle-Feez the Magic Carpet Smeevil", "fezzle-feez_the_magic_carpet_smeevil"],
      ["Anti-Mage", "anti-mage"],
      ["Hwytty & Shyzzyrd", "hwytty__shyzzyrd"],
      ["Na'Vi's Weaselcrow", "navis_weaselcrow"],
      ["Aghanim’s Baby Roshan", "aghanims_baby_roshan"],
   ];
   for (const [name, token] of cases) {
      test(name, () => assert.equal(getThumbUrl(o(), name), `https://cdn/${token}.webp`));
   }

   test("default/disable choices and a missing pattern have no thumbnail", () => {
      for (const n of ["Default Courier", "Disable Shader", "  default weather", "DISABLE SPECIAL"]) assert.equal(isDefaultChoice(n), true);
      assert.equal(isDefaultChoice("Crownfall"), false);
      assert.equal(getThumbUrl(o(), "Default Courier"), null);
      assert.equal(getThumbUrl(o({ thumbnailPattern: "" }), "Crownfall"), null);
   });

   test("a thumbnailId override wins, on choices and on styles", () => {
      const withIds = o({
         choices: [{ id: "Crownfall", name: "Crownfall", thumbnailId: "shared", styles: [{ id: "Crownfall (Style 1)", name: "x", thumbnailId: "s1" }] }],
      });
      assert.equal(getThumbUrl(withIds, "Crownfall"), "https://cdn/shared.webp");
      assert.equal(getThumbUrl(withIds, "Crownfall (Style 1)"), "https://cdn/s1.webp");
   });
});
