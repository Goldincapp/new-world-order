# New World Order: complete game overview

A handoff document for anyone (human or AI agent) picking up this project. It covers what the game is, how it plays from minute one to the endgame, every system with its real numbers, how the server and client are built, and how to run, test and deploy. Numbers here are read from the code as of 2026-10-07; when in doubt, the code wins.

---

## 1. The pitch

**New World Order** is a real-time, persistent, mobile-first strategy MMO set on a fictionalised real-world map (no real country names). An AI called **the Caretaker** has run the world since the old order collapsed. It hands out rations, taxes trade, patrols with drones and keeps a permanent **record** of how every player behaves. Players arrive as settlers, build a walled home base, claim land, trade in a player-run economy, smuggle or ship legally, vote and stage coups, form alliances, and gradually push the Caretaker out of their sector. Together the server must beat **the Sentinel**, a raid boss guarding Region 2 (**the Ashlands**), where land is richer and nations fight over every parcel.

Tone: grounded, a little bleak, dry. The Caretaker speaks in short, cold lines ("Taxes received. Order is expensive, and you are paying for it.").

- **Live server:** https://game-server-production-8951.up.railway.app (hosted on Railway)
- **Repo:** https://github.com/Goldincapp/new-world-order (branch `main`)

---

## 2. Tech stack and repo layout

| Part | Tech |
|---|---|
| Server | C# / ASP.NET Core 8 minimal APIs, SignalR hub at `/hub`, EF Core with SQLite, EF migrations applied on start |
| Client | A single-file web app `client/index.html` (about 4,000 very long lines), plus the operations in `client/operations.js` (2D) and `client/operations-3d.js` (optional 3D). three.js r160 from jsDelivr via an import map, SignalR browser client, no build step |
| 3D assets | `client/models/`: Kenney CC0 kits (industrial, commercial, suburban, nature, car, castle) plus unit models from poly.pizza. Credits are in `client/models/CREDITS.txt`; the soldier model is CC-BY |
| Hosting | Railway service `game-server`, built from the `Dockerfile`. The SQLite database lives on a Railway volume (`NWO_DB`) |

```
client/                 The game players use (served by the server too)
  index.html            Everything: UI, 3D views, networking
  config.js             window.NWO_API (empty = same origin)
  models/               glTF model packs
  operations.js         The three operations (2D canvas) and their menu, briefings and results
  operations-3d.js      Optional 3D view for Leviathan Assault
  assets/operations/    Operation sprites and arena backdrops (rendered by tools/blender)
server/NWO.Server/
  Program.cs            Startup, REST endpoints, admin endpoints, DTOs
  Data/GameDb.cs        EF Core model (all entities)
  Data/Migrations/      EF migrations (never edit applied ones)
  Game/*.cs             One file per game system (see section 5)
prototype/              The original single-file demo, kept for reference
docs/                   This file; PLAYTEST_AUDIT.md (the playtest tools); EARLY_GAME_PROGRESSION.md (onboarding design)
tools/
  check-client.mjs      Syntax check for the client (must pass before finishing)
  blender/              Scripts that build and render the Wasteland and operation art
  playtest/             Read-only playtest audit and the Playtest Lab (see section 9)
Dockerfile              Railway build
```

Core server patterns:

- **`World.Locked(async db => ...)`** runs a unit of game logic under a global lock and saves. Every state change goes through it.
- **`Ledger.Add(db, player, resource, delta, reason)`** is the only way balances change. Every change is recorded.
- **`Caretaker.Remember(db, player, delta, kind, text)`** is the only way standing changes. It writes a record line and the Caretaker comments live.
- **Production is computed from timestamps, never ticked.** Idle land costs nothing; collecting banks what accrued.
- **Battles are simulated on the server.** The client only sends commands (deploy, strike, flak), so results can't be faked.

---

## 3. The game loop

### 3a. The player's loop

**First session (about 15 minutes)**

1. **Sign up.** Pick a name (3 to 16 letters, numbers, `._-`). No email or password: the device keeps a secret token, and a year-long cookie backs it up. The player gets a home plot in the least-crowded open Region 1 sector.
2. **Survival chapter (8 focused objectives).** The opening uses a problem → action → visible reward rhythm: build a Barracks, train five gunners, defend the base in Last Stand, reinforce the walls the raiders reached, meet the settlers, add food and power, then collect the base's first production. Only the controls and resource counters needed for the current objective are shown; the wider interface unfolds after the chapter. Every objective names its reward before the player acts.
3. **Field Guide (14 objectives grouped into chapters).** Claim land → build a terrain-matched structure → Extraction Run → upgrade HQ → research → upgrade the Barracks → market → legal delivery → first battle → Leviathan Assault → alliance → vote → Caretaker record → Sentinel. Alliance and voting objectives can be passed individually, or the player can end the guide while keeping every system available.
4. **Chaptered Field Guide.** Expansion, Growth, Trade, Defence, World Threats and the Caretaker each introduce one new system through a concrete action. Community and Leadership are explicitly optional so an empty server cannot block a solo tester. Each prompt defines the new term, gives one direct call to action and points at the relevant control: a gold callout with a bouncing arrow sits beside the button, or beside a building in the 3D view, and places itself so it doesn't cover the target or the tutorial card. The rest of the screen is no longer darkened. Three mobile action games break up that strategy path. Last Stand now appears during Survival, immediately after the Barracks, so the building has an understandable purpose and an instant payoff. Extraction Run and Leviathan Assault arrive later as tests of expansion and world-threat readiness. Completed Operations remain replayable without repeat tutorial payouts.

**Every session after that (the core loop, 5 to 15 minutes)**

1. **Come back.** Opening the game auto-collects everything made while away (if away 60 seconds or more), up to storage limits: 8 hours base, plus 2 hours per warehouse. The welcome card says what was made.
2. **Spend.** Claim parcels (2,500 cash), build on them, place or expand home buildings, start research, upgrade the HQ.
3. **Trade and ship.** Sell surplus oil, grain or fuel on the market. Take a delivery contract by the legal road (taxed, safe) or the back road (untaxed, risk of checkpoints and drones). Inspect other players' trucks for profit.
4. **Fight.** Clear militia camps, ambush patrols, raid rival garrisons, attack AI or neighbour bases (3 stars conquers a parcel), and chip at the Caretaker's outpost with your sector.
5. **Social.** Alliance chat and bank, research help, elections and laws, coups.
6. **Leave.** Land keeps producing up to the storage cap, so there's a reason to return within about 8 to 10 hours.

**The long arc (days to weeks)**

- **Grow** from one parcel to dozens. Specialise in a research branch, upgrade the HQ (more buildings, bigger expansions up to 26×26) and train defenders.
- **Free your sector.** Develop it, petition, and siege the Caretaker's outpost together until its presence hits 0%. Name the sector by holding 10% of its land.
- **The Sentinel.** On the season timeline (default 21 days after season start), the Sentinel appears at the Region 2 fog wall. Everyone joins one shared 15-minute battle; it takes 10 to 15 coordinated players to win. A win opens the Ashlands for everyone, with a 12-hour head start for the gatebreakers.
- **The Ashlands.** Land there yields 1.5× but anyone can seize it by beating its garrison. Players found nations, declare wars and fight for war score.

### 3b. The server's loops (BackgroundServices)

| Service | Tick | What it does each tick |
|---|---|---|
| `Battles` | 100 ms | Steps every live battle (lane or arena), broadcasts state, ends finished battles and applies results |
| `Siege` | 100 ms | Runs the Sentinel siege when active; checks the season timeline and retry timer |
| `Logistics` | 1 s | Moves trucks, rolls checkpoints and audits, delivers cargo; then flushes queued Caretaker memories and runs the politics clock (elections, law votes, coups) |
| `Research` | 5 s | Completes research whose time is up and tells the player |
| `Presence` | 60 s | Recomputes the Caretaker's presence per sector from development, counts real settlers, repairs damaged outposts, announces freed sectors |
| `Bots` | 60 to 100 s | Moves about 1 in 25 AI settlers (collect, claim, build, donate, research); may run one raider attack and one ally siege move |

Production, storage, rations and HQ effects are not ticked: they're computed from timestamps when someone looks.

---

## 4. The world

- **Region 1 is one seamless map in the shape of Aurelia,** which is the outline of Germany on the old world map (`NationMap.cs`).
  - **Sectors:** the 50 sectors are 34×34-parcel squares laid over that outline (45 parcels per degree), numbered in reading order from the north-west. Sector 8, the capital, is the square holding old Berlin.
  - **Border wilds:** parcels outside the border can't be claimed. Strips of the country that no square covers are border wilds.
  - **Closed sectors:** 3, 5, 8, 18, 30, 44 and 46. Sector 8 is the capital, 18 and 44 are rival garrisons, and 3, 5, 30 and 46 are Caretaker land.
  - **The other 43 are open to settlers.**
- **The land follows the real geography** (`Geography.cs`), so players recognise where their land would be in the real world:
  - **Water:** real rivers (Rhine, Elbe, Danube, Main and others) and lakes are water.
  - **Mountains:** mountain ranges raise the land (height 0 to 9, snow on the Alps) and are rocky or wooded.
  - **Areas:** farm plains, forests, heath and oil areas set the mix of land.
  - **Old towns:** the old towns stand as ruins where they really were.
  - **Roads:** a road network joins the towns, and every sector hall joins the network.
  - **No real names:** players never see real place names. The names in `Geography.cs` are only for reading the code.
- **Each sector is a 34×34 parcel grid**, mostly barren, with pockets of other land generated from its place on the map.
  - **Land codes:** `ba` barren, `sc` scrub, `gr` grassland, `fe` fertile, `ro` rocky, `oi` oil sands, `ru` ruins, `wa` water, `pa` road, `ha` sector hall, `xx` beyond the border (and `xs` sea in the atlas).
  - **Resources under a parcel:** oil, grain, timber, ore, stone, housing, salvage or none.
- **The rest of the region (Europe and its edges) is on the same map,** so every nation can be looked at the way Aurelia can (`WorldAtlas.cs`).
  - **What it shows:** real country shapes (from `client/nwo-geo.js`, the same ones the Nation view draws), the major rivers, ranges and lakes, old cities as ruins, roads, and broad climate (boreal north, dry Mediterranean south, eastern steppe, desert below the Atlas). Land just beyond Aurelia's border is real foreign land, not grey.
  - **Settling:** only Aurelia's sectors can be settled. Tapping foreign land shows whose nation it is.
  - **Data:** `GET /api/atlas/overview` returns a coarse map of the whole region (one sample every 8 parcels, about 11 KB compressed). `GET /api/atlas/squares?c=&r=` returns a 3×3 block of detailed 34×34 squares as the camera moves.
  - **Zooming:** the Nation view hands over to the map anywhere in the region, not just over Aurelia.
- **`GET /api/nation`** returns the layout, the border, where the towns stand, and an atlas of every parcel's land code and height for the whole nation plus a 20-parcel margin of sea and foreign land (about 43 KB compressed).
- **The client's Sector view is the whole nation.**
  - **Boards:** nearby 34×34 squares (boards) are built in full and stream in and out as you pan.
  - **Far view:** a low-detail mesh of the whole map shows the nation from far away.
  - **Active sector:** the sector under the camera becomes the active one (its parcels, cards and live updates).
  - **Province button:** it rises to the whole-nation view.
  - **Code and other views:** see the `NAT` block in `client/index.html`. Classic graphics and the Ashlands still use the single-sector board.
- **Region 2, the Ashlands:** sectors 101 to 120 (4×5). They open after the Sentinel falls.
- **Nations:** Aurelia owns Region 1. Its delivery and market taxes go to Aurelia, which players govern. Players can found up to 8 more nations from Ashlands footholds.

---

## 5. Systems in detail

### 5.1 Accounts and sign-in (`Auth.cs`, `Program.cs`)

- **Guest accounts.** `POST /api/auth/guest {name}` returns a token; the server stores only its SHA-256 hash.
- **Remembered sign-in.** The token is kept in `localStorage` and in an HttpOnly year-long cookie `nwo_t`. If the page loses its copy, `/me` authenticates by cookie and hands the token back.
- **Recovery codes.** `POST /api/me/recovery` issues a 12-character code like `ABCD-EFGH-JKLM` (no look-alike letters). `POST /api/auth/recover {code}` signs in on another device and adds that device to `ExtraTokens` (keeps the last 6), so other devices stay signed in.
- **Returning players.** The client retries `/me` through bad connections (a "Reconnecting…" toast, then a Try again card). It only shows sign-up on a real 401. Devices that have played skip the intro.
- **Start over.** `POST /me/restart` resets base, land, money and tutorial; name, nation and record stay.

### 5.2 Resources and economy (`Economy.cs`, `Ledger.cs`)

- **Resources:** cash, oil, grain, fuel, gold, power. There's also a land counter (parcels owned), shown as a chip in the HUD.
- **Starting supplies:** defaults are 6,000 cash, 120 fuel and 50 gold. The live test server sets `NWO_START_CASH=100000`, `NWO_START_FUEL=1000` and `NWO_START_GOLD=500`.
- **Base income:** 40 cash an hour.
- **Land yield per parcel per hour**, by resource under it:

  | Resource | Yield |
  |---|---|
  | Oil | 24 oil |
  | Grain | 18 grain |
  | Timber | 30 cash |
  | Ore | 45 cash |
  | Stone | 35 cash |
  | Housing | 40 cash |
  | Salvage | 35 cash |
  | None | 10 cash |

- **Parcels:**
  - A claim costs 2,500 cash (4,000 in the Ashlands).
  - Each parcel has 9 building slots.
- **Parcel buildings:**

  | Building | Cost (cash) | Slots | Output per hour | Needs research |
  |---|---|---|---|---|
  | Housing | 800 | 2 | 60 cash | Housing blocks |
  | Farm | 600 | 3 | 24 grain | none |
  | Oil rig | 1,500 | 2 | 36 oil | none |
  | Sawmill | 700 | 2 | 30 cash | none |
  | Mine | 900 | 2 | 40 cash | Mining |
  | Warehouse | 1,000 | 3 | 10 cash | Warehousing |
  | Solar farm | 900 | 2 | 12 power | Solar arrays |
  | Wind turbine | 1,400 | 2 | 20 power | Wind power |

- **Suitability.** Anything can be built anywhere, but land decides output. A rig on oil sands runs at 1.0, on barren ground 0.06. A farm gets 1.5 on fertile land. Solar gets 1.25 on barren land.
- **HQ upgrade** costs 2,000 × level².
- **Caretaker ration crate:** every 20 hours, 20 to 110 grain and 0 to 25 fuel depending on standing tier.
- **Caretaker tithe:** up to 15% of everything made in a sector it still holds (see 5.6).

### 5.2a The nested economy: people, food, energy, work (`Society.cs`)

Every home base runs a small economy, and the same ledger adds up to the sector and the nation.
- **People.** Each base has a population, tracked on timestamps like production (`Player.Population`, `PopulationAt`). New players start with 4; players from before this existed start full.
- **Housing** caps it: 6 + 4 × HQ level, +4 per Barracks, +12 per Housing building on your land.
- **Needs.** Each person eats 0.4 grain and uses 0.25 power an hour, taken out of production, so rates can go negative. Consumption only ever takes what is in store, so balances never go below zero.
- **HQ generator.** The HQ makes (6 + 4 × HQ level) × 0.25 power an hour, enough for the people the HQ itself houses. Growing past that needs solar or wind.
- **Growth.** While food and power hold (this hour's balance is positive, or there is some left in store) the population closes most of the gap to its housing in about 6 hours. If either runs out it shrinks 4% an hour, down to 2.
- **Work.** Buildings need workers: workshop 3, research lab 2, warehouse 1, crop plot, oil pump and generator 1 each; farm, rig, sawmill and mine 3, land warehouse 2, solar farm and wind turbine 1. Output runs from 80% with nobody to 100% fully staffed.
- **Sector and nation.** A sector's ledger is the sum of the real players' bases whose home is there (AI neighbours aren't counted); the nation's is the sum of all of them. Hub `EconomyView` returns all three.
- **Needs board.** When contracts are posted, 70% of new ones go to sectors short of food (a grain order) or power (a fuel order for generators), for about six hours of the shortfall rounded up to 50 (50 to 300), issued by "Hungry settlers" or the sector council. A sector gets at most one open order per shortage.
- **In the HUD.** A People chip (people/housing, red when food or power has run out) opens the Economy card, with Your base, Sector and Nation tabs.

### 5.3 Home base (`HomeBase.cs`, `Defense.cs`)

- **Moving home.** "Move my base here" (`RelocateBase`, `World.Relocate`) moves the base to any open Region 1 parcel: a free one, which is claimed as part of the move, or one you already own. It costs 5,000 cash and 50 fuel, plus the claim cost for a free parcel, and you can move once a day (once a minute with dev tools). The buildings come along, and the old home parcel stays yours as land. Parcels can be claimed in any open sector, not just your home sector.

- **The grid.** 26×26 overall. The unlocked square grows 14 → 18 → 22 → 26.
  - **Expansion costs:** 8k cash and 60 power, then 40k and 250 power, then 120k and 600 power.
  - **Yard:** a 3-tile strip outside the walls where field buildings can go.
  - **Fog:** beyond the yard is fog. It's drawn as white low-poly clouds in the new graphics.
- **HQ.** 3×3 in the centre.
- **Building limit:** 4 + 3 × HQ level buildings, +2 with Town planning research.
- **Walls** (`HomeBase.ReinforceWalls`, Reinforce walls on the HQ card). Wall strength, separate from the base's size, goes from level 1 to 5. Reaching level 2 costs 400 cash, then 2,000, then 6,000 and 40 power, then 15,000 and 120 power. Each level adds 25 to Last Stand's gate integrity (100 at level 1) and 10% to the base's tower and HQ health when it's raided. The tutorial's fourth step, right after Last Stand, has the player reinforce once (reward 200 cash).
- **Home buildings:**

  | Building | Cost (cash) | Size | Effect |
  |---|---|---|---|
  | Barracks | 600 | 2×2 | Trains and houses troops |
  | Warehouse | 500 | 2×2 | +2 h storage |
  | Research lab | 800 | 2×2 | +5% to all land output; hosts the tech tree |
  | Workshop | 500 | 2×2 | 25 cash/h |
  | Crop plot | 150 | 1×1, field | 5 grain/h |
  | Oil pump | 400 | 1×1, field | 6 oil/h, best on oil sands, at least 0.5× |
  | Solar panel | 350 | 1×1, field | 5 power/h |
  | Generator | 700 | 1×1, field | 4 fuel/h |

- **Building levels.** Every home building has its own level from 1 to 5 and cannot rise above the HQ level. Its model gains visible detail and a stronger silhouette with each level. Production buildings improve output; Warehouses add storage time; Research labs open higher research tiers; Barracks add defender capacity and unlock Gunners at level 1, Launchers at level 2, Tanks at level 3 and Helicopters at level 4.
- **The entrance.** The front wall has one full-width gate with a paved approach, labelled with the wall level. Its look follows the wall level through five stages: a scrap gate with amber beacons, then side buttresses and armour bands, a powered arch with status lamps, watch posts with close-range guns, and finally a second armoured gate forming a lit airlock.
- **Placement UI.** A ghost model appears at the nearest free spot by the HQ. Drag it or use the arrows, Rotate, then confirm. Green means it fits, red means blocked.
- **Defence:**
  - **Free garrison** sized by HQ level and barracks. Tower and HQ health also grow 10% per wall level.
  - **Doctrine** sets how much of the garrison patrols from the start: Heavy patrols 70%, Balanced 50%, Deep reserve 25%.
  - **Trained defenders** cost militia 15, gunner 40, launcher 90, tank 300, heli 400. Trained defenders killed in a raid are gone.

### 5.4 Research (`Research.cs`)

- **Shape.** 5 branches × 3 tiers. Players choose their path.
- **Off-path cost.** Every tech owned outside a branch adds 15% to the next tech's cost there.
- **Tier costs and times:** tier 1 is 1,500 cash and 10 min, tier 2 is 5,000 cash and 60 min, tier 3 is 15,000 cash + 30 gold and 240 min.
- **Help and test speed.** Alliance members can help: each help cuts 5% of what's left, up to 10 helps. `NWO_RESEARCH_SPEED` compresses time for tests.

| Branch | Tier 1 | Tier 2 | Tier 3 |
|---|---|---|---|
| Energy | Solar arrays (unlocks Solar farms) | Wind power (unlocks Wind turbines) | Grid storage (+25% power) |
| People | Housing blocks (unlocks Housing) | Irrigation (+25% grain) | Town planning (Housing +40%, +2 HQ buildings) |
| War | Drill sergeants (+25% troops) | Air support (+1 strike, +1 heli per battle) | Siege engineers (+20% vs Sentinel, enemy garrisons 20% smaller) |
| Industry | Mining (unlocks Mines) | Deep drilling (+25% oil) | Refining (+50% fuel) |
| Trade | Warehousing (unlocks Warehouses) | Haulage (halves the Caretaker's 5% fee) | Back channels (back-road trucks 30% less likely to be stopped) |

### 5.5 The Caretaker and standing (`Caretaker.cs`)

- **Standing.** Every player has standing (starts at 54). It changes only through `Caretaker.Remember`, which writes a permanent record line.
- **Tiers** (standing / 20): Threat, Disruptor, Observed, Steward, Trusted.
  - **Checkpoints:** inspection risk is 2.0×, 1.5×, 1.0×, 0.7× and 0.4× across the tiers.
  - **Rations** grow with tier.
  - **In base battles:** good standing may bring Caretaker sentries to your side; poor standing brings them against you.
- **Drones.** Three over each Caretaker sector and one over each settled sector. Players can shoot them down in a flak mini-game (20 fuel ammo, 180 salvage). They respawn in 2 hours and cost standing.
- **Militia camps.** Assault costs 100 fuel and must be won within 10 minutes. Clearing a camp keeps the sector safe for 24 h; the camp returns after that.
- **On the map.** Every sector's drones and militia camp are drawn on the seamless sector map as you scroll (they come with each sector's snapshot). Tap a drone to shoot it down, or a camp to assault it; the battle is fought on the camp's spot. Drones patrol their own stretch of the sector, and your standing sets how close they come: from 60 up, their patrol loop moves clear of your base (further the higher your standing, at least 5 to 13 parcels) and they don't hover over where you're looking; below 40 they drift over to circle your base and tail you around their sector; in between they ignore you.

### 5.6 Caretaker presence and outposts (`Presence.cs`)

Each open Region 1 sector has a **presence** from 100 (the Caretaker rules) to 0 (a free sector).

- **Its effect:**
  - Tithe of up to 15% of all output made in the sector, including home bases there.
  - Checkpoints at 0.4 to 1.0×.
  - Drones stop patrolling below 20% presence.
- **Develop.** The target falls as the sector develops:
  `target = 100 − 4×settlers − 0.5×parcels − 0.8×buildings − concessions`.
  Presence falls toward the target at 3%/h and rises back at 1%/h. Force alone doesn't last, but development does.
- **Petition.** Hold 3 parcels in the sector and pay 2,000 cash. The chance of success is standing/100. A success concedes 10% permanently, up to 40% from petitions. Once per sector per 24 h.
- **Outpost siege (shared):**
  - **Cost:** 100 fuel and 5 standing per attempt; a 3 h personal cooldown (5 min with dev tools).
  - **Persistent damage:** two towers and an HQ whose damage persists between attacks and repairs 4%/h, so attacks need to land close together.
  - **Strength scales with real settlers in the sector (bots excluded):**
    - Health is ×(1 + 0.35 per extra settler), capped at ×4.
    - Tower damage is ×(1 + 0.12 per extra settler), capped at ×2.
    - Troop counts are ×(1 + 0.1 per extra settler), capped at ×1.8.
  - **Payout when it falls:**
    - The Caretaker concedes 20% permanently and a fresh outpost stands at the new presence.
    - The final blow earns +1,500 cash.
    - Everyone who damaged it gets +1,000.
  - **Balance (simulated chain of assaults):** at 1 settler it takes about 3 attacks, at 3 settlers 5, at 6 settlers 8, at 10 settlers 11.
- **On the sector map.** One outpost pylon appears per 25% of presence still held. The sector chip shows "Caretaker N%" or "free".

### 5.7 Market (`Market.cs`)

- **Order book.** A player-run book for oil, grain and fuel. Orders match by best price, then by who came first.
  - **Limit orders** wait on the book.
  - **Market orders** fill what they can and drop the rest.
- **Escrow and fee.** Goods and cash sit in escrow until filled. Fee: 2%.
- **Caretaker standing orders.** It always buys and sells at the edges, with 1,000,000 depth on each side. This stops prices crashing or running away.

  | Commodity | Caretaker buys at | Caretaker sells at |
  |---|---|---|
  | Oil | 32 | 54 |
  | Grain | 12 | 24 |
  | Fuel | 48 | 80 |

### 5.8 Logistics: contracts, trucks, smuggling (`Logistics.cs`)

- **Contracts.** Sectors post contracts for goods; there are 6 open at a time. A contract in your own sector is a short haul from your base to the sector hall (taxed or smuggled like any other).
- **Ship.**
  - **Legal road:** pays the nation's delivery tax (8% default) and the Caretaker's 5% fee.
  - **Back road:** untaxed and 1.3× slower. It risks the Caretaker checkpoint: a 35% base chance, scaled by standing, drones over the destination, Back channels research and sector presence.
  - **Bribes:** a stopped truck can be bribed through by raising the bribe.
  - **Audits:** a 35% chance, with a 1,800 fine.
- **Inspection.**
  - **Costs and limits:** an inspection costs 150, up to 20 a day per player.
  - **Wrong calls:** searching a clean truck costs compensation. The first two cost 300 each, then every miss in a row doubles it.
  - **Envelopes:** smugglers can leave an envelope (a bribe to the inspector), which the inspector takes or refuses.
- **Trucks** use the car-kit models on the maps. On the seamless sector map every truck drives across the land from its sender's base (or sector hall) to the destination sector's hall: the main road runs fairly direct, the back road swings wide and winds through the countryside. Tap a truck for its card (inspect it, or track it); after you send one, the camera follows it until you drag the map.

### 5.9 Battles (`Battles.cs`, `BattleSim.cs`, `ArenaSim.cs`)

- **Lane battles (`BattleSim`):**
  - **Layout:** 3 lanes with towers at both ends.
  - **Command points:** they refill over time and pay for deploys. An artillery strike costs 6.
  - **Units (cost, HP, notes):**

    | Unit | Cost | HP | Notes |
    |---|---|---|---|
    | Gunner | 2 | 80 | Anti-air |
    | Launcher | 4 | 120 | Anti-vehicle |
    | Tank | 5 | 420 | |
    | Heli | 6 | 240 | |
    | Militia | 1 | 60 | |

  - **Enemy-only units:** raider, technical and mortar team.
  - **Counters:** tank ← launcher, heli ← gunner, gunner ← tank, launcher ← gunner.
  - **Starting troop stock:** up to 24 gunners, 10 launchers, 6 tanks, 3 helis, 30 militia and 3 strikes. Militia and strikes are available by default; a Barracks unlocks Gunners at level 1, Launchers at level 2, Tanks at level 3 and Helicopters at level 4. Air support research can add a Helicopter even before the Barracks unlock.
  - **Used for:** militia camps, ambushes (90 s against a patrol) and raids on rival garrisons (Blue Party sectors 18/44, with counter-play doctrine).
- **Base assault arena (`ArenaSim`, Clash Royale-like):**
  - **Layout:** deploy anywhere in your half against two guard towers and an HQ.
  - **Defenders:** a patrol on the field plus a reserve garrison that pours out. Caretaker sentries may join either side by standing.
  - **Stars:** one per tower destroyed; the HQ gives all 3.
  - **Used for:** attacks on AI and neighbour bases, and outpost sieges.
- **Kinds of battle:**
  - `camp`: militia camp, 100 fuel.
  - `raid`: rival garrison, loot only.
  - `ambush`: 90 s against a patrol.
  - `raidbase`: 40 fuel. Works against AI bases anywhere, or real players **in your own home sector**. Loot is 10/15/20% of cash, oil and grain by stars. **3 stars also conquers one of the defender's non-home parcels in that sector.** The defender gets an 8 h shield afterwards and is alerted in chat. New accounts are protected for 24 h (10 min with dev tools). Alliance members can't attack each other.
  - `seize`: an Ashlands parcel, 80 fuel (half at war). Not against your own nation or alliance.
  - `outpost`: see 5.6.
- **After a battle.** If the player doesn't press Leave, a watchdog returns them to the map automatically.

### 5.10 AI neighbours (`Bots.cs`)

- **Numbers.** `NWO_BOTS_PER_SECTOR` (default 3) × 43 open sectors gives about 129 AI settlers.
- **What they have.** Each has a home base, land, research and usually a membership in one of three AI alliances (Iron Wolves, Salt Road Traders, Ashen Vanguard). They don't chat.
- **Natures:** each sector gets one of each where possible.
  - **Ally:** joins a siege on its sector's outpost once a real player has started one (every 4 h at most), and accepts alliance invites 90% of the time, leaving its AI alliance.
  - **Raider:** attacks real neighbours' bases in its sector at most once per player per 12 h, every 8 h at most per raider. The fight is played on the server with `ArenaSim.AutoPlay`. Loot is 6/10/15% by stars (capped at 2,500 cash and 200 oil/grain); raiders take no land. Raiders respect newcomer and post-raid shields and never join player alliances.
  - **Trader:** grows quietly and accepts invites 50% of the time.
- **For players.** AI bases can be attacked like any base, and their card shows their nature.

### 5.11 Alliances (`Alliances.cs`)

- **Creating one.** Costs 5,000 cash and needs a name and a tag. Alliances can be open or closed.
- **Size and roles.** Up to 30 members. Roles are leader, officer and member.
- **Officer powers.** Officers invite players, accept requests and grant from the bank.
- **What members share:** a bank (cash and fuel donations), a private chat (only members receive it) and research help.
- **Protection.** Allies can't seize, raid or conquer each other.

### 5.12 Politics (`Politics.cs`)

- **Aurelia's government:**
  - **Elections:** a President is elected every 24 h (`NWO_ELECTION_MINUTES`). Standing costs a 1,000 deposit, and candidates give speeches.
  - **Laws:** cost 500 to propose and are voted on for 2 h. They set:
    - delivery tax (0 to 20%)
    - market tax (0 to 10%)
    - smuggling fine (25 to 150% of the cargo reward)
  - **Decrees and snap elections:** the President can pass a law by decree, at a cost of 10 to 15 legitimacy. Low legitimacy triggers a snap election.
- **New nations.** Founding one needs 3 Ashlands parcels, 10,000 cash and 50 gold; up to 8 player nations. Players can switch nation once per 24 h.
- **Wars.** Presidents declare war and make peace. At war, seizing enemy Ashlands land costs half the fuel and scores double.
- **Coups.** A coup costs 3,000 cash and 20 gold. Citizens have 1 h to back it; at least 2 backers are needed, more if the President is legitimate. A failed coup is remembered by the Caretaker.

### 5.13 The Sentinel siege (`Siege.cs`, `SentinelSim.cs`)

- **Shape.** One shared server-wide battle over 5 lanes, with a 15-minute limit. Base health is 2.6M (`NWO_SENTINEL_HP_MULT`), and it grows for every active fighter beyond 12.
- **Armour.** It only cracks when about 12 commanders hit within 10 s (`NWO_SENTINEL_FULL_ARMOR`); a lone player does almost nothing.
- **Phases:**
  1. **100% to 70%:** four relays shield it (damage cut to 15%). A telegraphed sweep beam clears a lane.
  2. **70% to 30%:** relays return in linked pairs that must fall within 6 s of each other. It marches forward and stomps, and heavy guards join.
  3. **Below 30%:** vents open, giving 3× damage windows. EMP pulses freeze command points and the drone swarm thickens.
- **Throughout.** Drones attack units and repair relays, so players fire flak at them (1 CP, 70% hit chance).
- **Schedule.** It appears 21 days into the season (`NWO_SENTINEL_AFTER_HOURS`) and retries after 60 min (`NWO_SENTINEL_RETRY_MINUTES`).
- **Practice sieges.** With dev tools on, practice sieges with AI allies are available; they carry no rewards.
- **Simulated balance.** 8 AI players lose; 12 to 20 win in 9 to 13 minutes.

### 5.14 The Ashlands, Region 2 (`Region2.cs`)

- **Land.** Sectors 101 to 120. Claims cost 4,000 and land yields 1.5×.
- **Protection.** Fresh claims and captures are protected for 6 h.
- **Seizing.** Any non-allied, non-compatriot player can seize a parcel by beating its garrison, which is sized by the owner's barracks and buildings.
- **War score.** Claims score 5, captures score 25, and every parcel held is worth 10. There's a leaderboard for nations and alliances.

### 5.15 Sector naming (`SectorNames.cs`)

Hold 10% of a sector's claimable land (about 98 parcels) to name it. Anyone who holds more land than the current namer can rename it.

### 5.16 Chat and feedback log

- **Channels:** global, nation and alliance (private). Max 280 characters, 1 message per second.
- **Feedback log.** Every player message is also written to `ChatLog`, which survives world wipes. Read it with `GET /api/admin/chatlog?since=&channel=&name=&format=csv|json`.
- **System messages.** System senders are "News", "Caretaker" and "Alert".

### 5.17 In-game feedback (`FeedbackDesk.cs`)

- **Sending.** A **Feedback** button in the side bar opens a panel with four kinds (bug, idea, too easy / too hard, other), a text box (2,000 characters) and an optional screenshot of the 3D view taken just before the panel opens. It posts to `POST /api/feedback` with context: view, sector, home sector, graphics setting, screen size and device. Limits: one every 20 s and 30 a day per player.
- **Storage.** Feedback is kept in the `Feedback` table and survives world wipes.
- **Reading it:**
  - `GET /api/admin/feedback?status=new&format=md` gives a Markdown list to hand to an agent.
  - `GET /api/admin/feedback/{id}/shot` returns the screenshot.
  - `POST /api/admin/feedback/{id}?status=seen|done|wontdo&note=` marks progress.
- **GitHub issues.** If `NWO_GITHUB_TOKEN` and `NWO_GITHUB_REPO` (`owner/repo`) are set, each piece of feedback also opens a GitHub issue labelled `feedback` and its kind. The player sees the issue link.

### 5.18 Operations: threat levels and rewards (`Operations.cs`, `client/operations.js`)

Three short action games: **Last Stand** (tutorial), **Extraction Run** and **Leviathan Assault** (Field Guide). They share one look: Blender-rendered arenas fitted to the screen's height, solid outlined projectiles with glow, damage numbers, red screen edges when hurt, and **Lumen**, a friendly AI drone that flies with the player in every operation and talks them through it.

- **Threat levels.** Each operation has its own threat level (`Player.OperationLevels`), starting at 1. Every win raises it by one, up to 30; losing keeps it. The level sets the enemies and the target. Player damage doesn't grow with it.
- **Rewards.** The guided run (the tutorial or Field Guide step) pays that step's reward, as before. Every other win pays a reward that grows 20% per level (up to five times the base at level 21), for the first **5 wins each UTC day** across all operations (`OperationPaidDay`, `OperationPaidWins`). Wins after that still raise the level.

  | Operation | Length | Target at level 1 | Target per level | Base win reward |
  |---|---|---|---|---|
  | Last Stand | 120 s | 12 raiders | +3 | 150 cash, 20 fuel |
  | Extraction Run | 50 s | 3 cargo | +2.5 (rounded down) | 120 cash, 40 fuel |
  | Leviathan Assault | 90 s | 1,800 damage | +120 | 250 cash, 1 gold (+1 gold per 4 levels) |

- **Balance.** Tuned with simulated players (`sim` harness, not in the repo): levels 1 to 3 can be passed with little input; a careful player tops out around level 12 to 16.
- **Art.** `tools/blender/leviathan_assault.py` and `tools/blender/operations_art.py` (sharing `ops_kit.py`) build the units and arenas; `leviathan_post.py` and `operations_post.py` add the glow. Sprites are used in the 2D view; the optional 3D view only has models for the Leviathan.

#### Last Stand

A tower defence with a hands-on turret. Raiders follow a winding road to the base's gate over 2 minutes: a 7-second build window, four waves with short lulls, and a War Rig (a boss truck) in the last 16 seconds. Vehicles are drawn from overhead and turned to follow the road, which the game rounds off at the bends; each keeps its own lane, eases off into turns, bounces and kicks up dust.

- **Scrap and towers.** Every raider stopped earns scrap (bike 2, technical 5, mortar 6, truck 8, War Rig 20; +10 at the start of each wave after the first; 35 to start). Tap one of the six pads to build, tap a tower to upgrade it (three levels):

  | Tower | Cost (I / II / III) | Does |
  |---|---|---|
  | Gun nest | 20 / 25 / 35 | Fast single shots at the raider furthest along the road |
  | Burner | 25 / 30 / 40 | Damages every raider close to it |
  | Rail gun | 35 / 35 / 45 | Long range, slow, heavy hits: for trucks and the War Rig |
  | Lumen beacon | 25 / 25 / 30 | Slows raiders in range (35 to 55%) and marks them to take 25 to 50% more damage |

- **The squad.** The defenders the player has trained (the tutorial trains five gunners; 3 to 8 shown) hold a sandbag line just before the gate. Raiders that reach it stop and fight them; trucks and the War Rig smash through. It's a drill: losses here don't count.
- **The HQ turret** fires by itself at raiders on the lower road; pressing a raider focuses it at any range and fires faster. Abilities: Overcharge (14 s: fire rate x2.5 for 4 s) and Lumen: Airstrike (10 s: 4 damage to the three raiders furthest along).
- **Raiders.** Bikes (6 gate damage); technicals that also shoot at the gate (level 2+); armoured trucks (level 3+, 20 gate damage); mortar teams that park mid-road and shell the turret platform, with a red warning ring before each shell (level 4+); the War Rig (40 gate damage). Raiders get 18% tougher per level and come faster; towers don't, so later levels need upgrades and good placement. At level 1 a gun nest is already built.
- **Losing.** Gate integrity starts at 100, plus 25 per wall level above 1 (reinforcement plates show on the gate); at 0 the run ends. The road and pad positions are shared by `client/operations.js` and `tools/blender/operations_art.py` (keep them in step).

#### Extraction Run

Drive the rover over crates in a Caretaker supply yard; the beacon opens in the final 12 seconds: reach the pad or press Extract. Running out of time without extracting scores 0. The distinctive black cache is worth 3 cargo at first and gains value after Threat 20, when the arena has reached its safe crate-density limit. This keeps every server target achievable without filling the yard with overlapping pickups.

- **Getting hit** drops the last crate: it's flung clear and the Caretaker reclaims it after 2.5 s unless you grab it back. Rams knock the rover clear and stall it briefly.
- **Abilities.** Nitro (6 s: faster and can't be hit for 1.6 s) and Lumen: Scramble (12 s: blinds patrols, sentries and drones for 3.5 s and clears their shots).
- **Defences.** Patrols with vision cones that chase when they see you (from level 1, more every 2 levels); sentries with telegraphed shots (level 2+); patrols that shoot (level 3+); searchlight drones that alert every patrol (level 4+). Crates: 8 plus 1.5 per level; one black cache worth 3.

#### Leviathan Assault

A 90-second boss fight against a Caretaker war-mech, unlocked by the Field Guide's World threats chapter (guided reward 800 cash and 10 gold).

- **The team.** The player is a strike leader on foot, with Lumen circling them. The leader's rifle fires a fan of rounds by itself (3 rounds, widening to 5 in phase 2), and Lumen fires its own bolts at the mech's core. Hits on the glass dome are critical (double damage).
- **Abilities.** Missile Salvo (6 s cooldown, two homing missiles) and Lumen: Overwatch (12 s: a 2.6 s shield that also scrambles every enemy shot on screen).
- **Phases** (30 s each): Hunt, Crossfire, then Overheat, when the mech turns crimson and fires extra aimed shots between attacks.
- **Attacks**, one at a time with a named warning:

  | Attack | From | What it does | Hit |
  |---|---|---|---|
  | Targeting Burst | phase 1 | Aimed shots from the arm cannons (one more every 5 levels) | 9 to 11 |
  | Tithe Spiral | phase 1 | Orbs spiral out of the core (an extra arm from level 6) | 9 to 11 |
  | Quarantine Grid | phase 1 | 4 to 6 of 12 floor tiles light up, then burn (your tile is always one) | 15 |
  | Compliance Scan | phase 1 | A beam sweeps the half of the floor you're on (both halves in turn in phase 3) | 16 |
  | Enforcer Discs | phase 2 | Spinning blades that bounce off the walls three times | 14 |
  | Breach Wall | phase 2 | A falling wall with one safe lane | 12 |

- **Losing.** Armour starts at 100; at 0 the run ends.
- **Art.** `tools/blender/leviathan_assault.py` builds the mech, leader, Lumen and the arena (sprites for the 2D view, GLBs for the optional 3D view); `tools/blender/leviathan_post.py` adds the glow. The arena backdrop is drawn to the screen's height, so the floor keeps its shape on any screen.

---

## 6. Client (`client/index.html`)

**Views** (bottom nav: Sector, Province, Nation, Home, World, Politics):

| View | Internal mode | Real or demo |
|---|---|---|
| Sector | `county` (`NET.big` = the 34×34 board) | Real: parcels, bases, AI, outposts, hall |
| Province | `province` | Region 1's 10×5 grid with demo decoration. When signed in, your real home sector gets the gold outline and label, and demo owners are hidden |
| Nation | `region` | Mostly prototype content (named nations, fog, wars) |
| Home | `hq` | Real home base |
| World | `world` | Globe from the prototype, with shipping lanes |
| Battle / Siege | `battle` / `siege` | Real, driven by server state |

Before sign-in (or in the offline demo), the old prototype county appears. It's labelled Sector 12 with a pre-built base and isn't the player's real state.

**Cards.** Building and information cards (the panel that opens when you press something) sit at the left of the screen with a header and a gold emblem, show stats as labelled rows, and scale their text with the screen. Long values move onto their own line and wrap instead of being cut off.

**Graphics:**
- **"Graphics: New"** (the default), toggled from the welcome card, applies to Sector, Province, Home and battles. It's a diorama look:
  - Sky gradient with light haze, no tone mapping, warm sun with soft shadows.
  - A colour grade (saturation 1.14, warmth, slight contrast, vignette) and a light tilt-shift.
  - Smooth vertex-coloured terrain with grass, sand and rock detail textures splatted by land type, darker hollows and rocky slopes.
  - Depth-shaded water with glints and foam.
  - Code-built low-poly pines, oaks, bushes, rocks, swaying grass and flowers, and cloud puffs.
  - **Battles sit in the real land.** Lane battles and base assaults are fought on a levelled clearing (rounded, with an uneven edge) cut into the nation map around where the fight is: the parcel you tapped, or a fixed dry spot in the sector. The land, trees, ruins, crops and water around the field are the same as on the sector map. The lanes are worn dirt paths, and a defender's yard is an irregular patch of trampled earth ringed by loose sandbags. Without the nation map (offline, or Classic), the old floating field is used.
- **"Graphics: Wasteland"** (third option on the same button, or `?gfx=waste`) re-dresses the same world as a grey, ruined, machine-watched wasteland: ash and dirt ground with broken concrete only in old towns, roads and harbours; dead olive-grey trees and scrub with no flowers; dry fields; dark murky water; a dusty grey sky and haze; a harder sun; a cooler, less saturated grade; no tilt-shift; concrete rubble scattered over the land, ruined concrete and brick shells on old-town parcels, burnt-out cars on old roads and a few small flickering fires; a grey concrete home base with dust clouds. The geography and gameplay are unchanged.
  - **Blender assets.** Ruined concrete and brick buildings, rubble piles, burnt-out cars and the Caretaker turret (which replaces outpost pylons) are modelled and baked in Blender by `tools/blender/wasteland_assets.py` (run headless: `blender -b --python tools/blender/wasteland_assets.py -- client/models/waste <preview.png>`), exported to `client/models/waste/*.glb` with the wear, rust and crevice dirt baked into one texture each, and only loaded in Wasteland mode. Until they load, simple box versions stand in. Preview: `docs/art/wasteland-preview.png`.
  - **Detailed home base.** `tools/blender/wasteland_base.py` (sharing the parts kit and materials in `tools/blender/wl_kit.py`) builds the Wasteland HQ (plated concrete command post with barred windows, hazard door, pipes, ladder, AC units, gun nest, water tank, antenna, roof clutter), a corrugated barracks hut, concrete-barrier wall segments with scrap tops and wire, a hazard gate, scaffold watchtowers and clutter props (crates, barrels, tyres, junk). Colour and bump detail are baked (colour + normal map). In Wasteland mode they replace the HQ, barracks, castle walls and towers; props sit in the inside corners and round the outside of the yard; the floor inside the walls is cracked concrete. Wasteland models render non-metallic in the game (the metal look is baked in). Preview: `docs/art/wasteland-base-preview.png`.
  - **Every home building.** `tools/blender/wasteland_buildings.py` builds the rest: a corrugated warehouse with a hazard-framed roller door and loading dock; a research bunker with glowing screens, a satellite dish, an antenna array and server cabinets; an open workshop with a bench, tool board, engine on a stand and hoist gantry; a diesel generator with louvres, exhaust stack and fuel tank; salvaged solar panels with a battery box; an oil pumpjack (static in Wasteland); and raised crop beds with a fence and irrigation pipe. Preview: `docs/art/wasteland-buildings-preview.png`.
  - **Sector map pieces.** `tools/blender/wasteland_map.py` builds a walled compound for each player base on the sector map (barrier walls, hazard gate, command block, watchtower; the owner's colour stays on its flag and a gate banner) and a fortified sector hall (columned civic building with a clock, banners, floodlights and barricades). Preview: `docs/art/wasteland-map-preview.png`.
  - **Interface skin.** In Wasteland mode the page gets `html.waste`: panels are dark steel plates with a hazard-striped top and corner rivets, headings use Saira Stencil One, the HUD and nav use Barlow Condensed in capitals, gold accents become hazard yellow, the active nav tab is hazard-striped, and every light surface inside panels is darkened.
- **Unchanged views.** Nation and World keep the classic render.
- **"Classic"** restores the old look everywhere. `?classic=1` forces it for one load.

**Debug URL parameters:**

| Parameter | Effect |
|---|---|
| `?ov=1` | Sector overview |
| `hb=1` | Open the home base |
| `mv=province\|nation\|world\|pol\|tech\|ally` | Open a view or panel |
| `sv=1` | Join the siege |
| `rb=<name>&auto=1` | Attack that base, with auto-deploy |
| `place=<type>` | Start placing a building |
| `dbg2=1` | Pin the camera and render every 2 s (headless Chrome pauses `requestAnimationFrame`) |
| `classic=1` | Classic graphics |

**Editing the client.** The file is huge with long lines. Make exact-string replacements (a node script with a `rep(a,b)` that throws unless `a` occurs exactly once), then syntax-check the module script by compiling it with `new AsyncFunction(code)` after stripping `import` lines.

---

## 7. Server API

**REST (`/api`):**

| Group | Endpoints |
|---|---|
| Health | `GET /health` |
| Sign-in | `POST /auth/guest`, `POST /auth/recover` |
| Your account | `GET /me` (auto-collects), `POST /collect`, `POST /me/recovery`, `POST /me/restart` |
| World data | `GET /sentinel`, `GET /sectors/names`, `GET /region2`, `GET /sector/{n}/map`, `GET /chat/{channel}` |

**Admin** (header `X-Admin-Key`):

| Endpoint | What it does |
|---|---|
| `POST /admin/wipe?confirm=WIPE` | New season: deletes all players and state, keeps the chat log |
| `GET /admin/players` | Lists real players |
| `POST /admin/reset-player?name=&confirm=RESET` | Deletes one account completely |
| `POST /admin/grant?name=&cash=&fuel=&gold=&oil=&grain=` | Gives a player resources |
| `GET /admin/chatlog` | Reads the feedback log |
| `POST /admin/region2/open` | Opens the Ashlands without a Sentinel win |
| `POST /admin/sentinel/start` | Summons the Sentinel |
| `POST /admin/sentinel/simulate?bots=` | Fast-forwards a siege with AI players |
| `GET /admin/sentinel/history` | Recent sieges |
| `POST /admin/arena/simulate?hq&barracks&doctrine&trained&runs&outpost&settlers&chain` | Arena balance testing; `chain=true` carries outpost damage between runs |

**SignalR hub (`/hub`, `access_token` query, header or cookie).** Methods grouped by system; see `GameHub.cs`:

| System | Methods |
|---|---|
| Sector | `WatchSector`, `VisitSector`, `Claim`, `Build`, `NameSector`, `PetitionCaretaker` |
| Home | `PlaceHome`, `MoveHome`, `ExpandHome`, `UpgradeHq`, `GetDefense`, `SetDefense`, `TrainDefenders` |
| Battles | `StartBattle(kind, sector, rivalKey, deck)`, `Deploy`, `DeployAt`, `Strike`, `Retreat` |
| Siege | `JoinSiege`, `SiegeDeploy`, `SiegeStrike`, `SiegeFlak`, `SiegeStatus`, `StartPracticeSiege`, `StopPracticeSiege` |
| Market | `WatchMarket`, `MyBook`, `PlaceOrder`, `CancelOrder` |
| Logistics | `WatchShipping`, `MyShipping`, `Ship`, `Inspect`, `ResolveEnvelope`, `RaiseBribe` |
| Caretaker | `MyRecord`, `ClaimRation`, `StartDroneHunt`, `ShootDrone` |
| Politics | `GetPolitics`, `RunForOffice`, `GiveSpeech`, `VoteFor`, `ProposeLaw`, `VoteLaw`, `FoundNation`, `JoinNation`, `DeclareWar`, `MakePeace`, `StartCoup`, `BackCoup` |
| Alliances | `GetAlliance`, `CreateAlliance`, `InviteToAlliance`, `RequestAlliance`, `AnswerAlliance`, `LeaveAlliance`, `KickFromAlliance`, `SetAllianceRole`, `EditAlliance`, `DonateToAlliance`, `GrantFromAlliance`, `HelpAlliance`, `AllianceChat` |
| Research and guide | `StartResearch`, `GuideSeen`, `SkipGuide` |
| Chat | `SendChat` |

Server → client events include:

| Area | Events |
|---|---|
| Player and world | `me`, `parcel`, `presence`, `sectorName` |
| Economy | `market`, `ship`, `contracts`, `stopped` |
| Caretaker | `record`, `drone`, `camp`, `notice` |
| Battles and siege | `battle`, `battleEnd`, `siege`, `siegeStart`, `siegeEnd`, `siegeMe` |
| Social | `chat`, `politics`, `alliance`, `allianceNote`, `research` |

---

## 8. Data model (`Data/GameDb.cs`)

| Area | Entities |
|---|---|
| Players | **Player** (resources, standing, HQ level, wall level, home sector, tutorial and guide step, techs, research, alliance and role, nation, defence doctrine and trained defenders, raid timestamps, token hash plus `ExtraTokens`, recovery hash, `IsBot`), **HomeTile** |
| Land | **Parcel** (sector, i, j, resource, buildings CSV, owner, `IsHome`, claimed at) |
| Economy | **LedgerEntry**, **Order**, **Trade**, **Contract**, **Shipment** |
| Caretaker | **RecordEntry**, **Drone**, **Camp**, **SectorPresence** (presence, target, concessions, petition and assault times, outpost T1/T2/HQ fractions, contributors) |
| Politics | **Nation**, **Candidate**, **Ballot**, **Law**, **LawVote**, **War**, **Coup** |
| Social | **Alliance**, **AllianceInvite**, **ChatMessage**, **ChatLog**, **SectorName** |
| Server | **ServerState** (season start, Sentinel schedule, Region 2 open, gatebreakers) |

**Schema changes.**

1. Add the property.
2. Run `dotnet ef migrations add <Name> -o Data/Migrations` from `server/NWO.Server`.
3. Rebuild.
4. Check the generated default values: new non-null doubles default to 0 for existing rows. This already caused a real bug, so set `defaultValue` where 0 is wrong.

---

## 9. Running, testing, deploying

**Run locally (Git Bash, Windows):**

```bash
cd server/NWO.Server
dotnet build
```

```bash
NWO_DB=../data/nwo.db NWO_ADMIN_KEY=localdev NWO_DEV_TOOLS=1 NWO_START_CASH=100000 dotnet run --no-build --urls http://localhost:5080
```

Open http://localhost:5080. The .NET SDK is installed per-user at `%LOCALAPPDATA%\Microsoft\dotnet`.

**Testing approaches used so far:**
- **Hub scripts.** Run in a browser on localhost using the page's `signalR` client: create a guest with `fetch('/api/auth/guest')`, open a `HubConnection` with its token, then invoke methods.
- **Database edits.** Set up scenarios with `node:sqlite` directly on the local database.
- **Headless screenshots.** Chrome with `--headless=new --use-angle=swiftshader`, using a temporary `client/_login.html` that stores a token from the URL hash (it's git-ignored; delete it after use) and `dbg2=1`.
- **Balance.** Use the admin simulators.
- **Playtest audit.** `node tools/playtest/audit.mjs` is read-only: it checks client syntax, that every operation artwork file exists, the operation balance and economy tables, and the tutorial's affordability, then writes an HTML and JSON report to `tools/playtest/output/` (git-ignored). `node tools/playtest/serve.mjs` runs the Playtest Lab on port 5090 (PC and phone): the report, a live onboarding storyboard, a phone performance benchmark and a feedback form. See `docs/PLAYTEST_AUDIT.md`.

**Deploy.** Railway CLI, run from the `server` folder:

```bash
MSYS_NO_PATHCONV=1 npx --yes @railway/cli@latest up --service game-server --ci
```

Railway is not yet linked to the GitHub repo, so pushing to GitHub doesn't deploy.

**Environment variables:**

| Variable | Default | Purpose |
|---|---|---|
| `NWO_DB` | `nwo.db` | SQLite path (on Railway: the volume) |
| `NWO_ADMIN_KEY` | none | Enables admin endpoints. **Secret, never commit or print it.** Locally kept in git-ignored `server/.admin-key` |
| `NWO_DEV_TOOLS` | off | Practice sieges, 10-minute newcomer shield, 5-minute outpost cooldown. **On** for the live test server; turn off before real players arrive |
| `NWO_START_CASH` / `_FUEL` / `_GOLD` | 6000 / 120 / 50 | Starting supplies (live test server: 100000 / 1000 / 500) |
| `NWO_BOTS_PER_SECTOR` | 3 | AI settlers per open sector (0 = none) |
| `NWO_RESEARCH_SPEED` | 1 | Research time divider for tests |
| `NWO_ELECTION_MINUTES` / `NWO_LAW_MINUTES` | 1440 / 120 | Politics timers |
| `NWO_SENTINEL_AFTER_HOURS` | 504 | When the Sentinel first appears |
| `NWO_SENTINEL_RETRY_MINUTES` | 60 | Retry after a failed siege |
| `NWO_SENTINEL_HP_MULT` / `NWO_SENTINEL_FULL_ARMOR` | 1 / 12 | Sentinel tuning |
| `NWO_ORIGINS`, `NWO_CLIENT` | none | CORS origins and client path for split hosting |

---

## 10. Status, known issues, ideas

**Working and live:** everything in section 5.

**Known issues and gaps:**
- **Tapping the sector hall** can open the parcel behind it, because the ray hits the terrain rather than the hall model.
- **Nation and World views** are largely prototype or demo content (invented nations, routes). Province decoration is demo too: only your home marker and the sector cards are real.
- **Test settings are still on live:** dev tools, generous starting supplies and AI settlers.
- **Graphics gaps:** the camera is perspective, not the reference style's orthographic diorama camera, and the HUD hasn't been restyled (navy panels with gold trim).
- **Untested end to end:** an outpost falling to a group, and the salvage being shared.

**Proposed next (design, not built yet):**
- **Nested economy, next steps.** The first version is built (see 5.2a and [`docs/nested-economy.html`](nested-economy.html)). Still to do: delivering food or fuel to a short sector should actually feed its homes, national food and power balances should move market prices, and the government should be able to spend the treasury on services.

**Rules for anyone working on this:**
- **Secrets:** never commit or print secrets (the admin key; any third-party keys are read from env vars only).
- **Destructive operations:** wipes and account resets delete real players' progress. Only do them when the owner asks.
- **Code style:** match the surrounding code. Short doc comments explain the *why*, and game text is written plainly, in British-leaning spelling ("defence", "armour").
