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
| Client | A single-file web app `client/index.html` (about 3,100 very long lines). three.js r160 from jsDelivr via an import map, SignalR browser client, no build step |
| 3D assets | `client/models/`: Kenney CC0 kits (industrial, commercial, suburban, nature, car, castle) plus unit models from poly.pizza. Credits are in `client/models/CREDITS.txt`; the soldier model is CC-BY |
| Hosting | Railway service `game-server`, built from the `Dockerfile`. The SQLite database lives on a Railway volume (`NWO_DB`) |

```
client/                 The game players use (served by the server too)
  index.html            Everything: UI, 3D views, networking
  config.js             window.NWO_API (empty = same origin)
  models/               glTF model packs
server/NWO.Server/
  Program.cs            Startup, REST endpoints, admin endpoints, DTOs
  Data/GameDb.cs        EF Core model (all entities)
  Data/Migrations/      EF migrations (never edit applied ones)
  Game/*.cs             One file per game system (see section 5)
prototype/              The original single-file demo, kept for reference
docs/                   This file
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
2. **Home base tutorial (7 steps, each pays cash).** Inside a 14×14 walled grid: place a Barracks, Warehouse, Research lab, Crop plot, Oil pump and Solar panel, then Collect. Starting cash covers all of it.
3. **Field Guide (11 chapters, each pays and can be skipped).** Claim land → build on it → upgrade HQ → pick a research path → place a market order → ship a delivery → fight a battle → join an alliance → vote → read your Caretaker record → learn about the Sentinel.

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

### 5.3 Home base (`HomeBase.cs`, `Defense.cs`)

- **Moving home.** "Move my base here" (`RelocateBase`, `World.Relocate`) moves the base to any open Region 1 parcel: a free one, which is claimed as part of the move, or one you already own. It costs 5,000 cash and 50 fuel, plus the claim cost for a free parcel, and you can move once a day (once a minute with dev tools). The buildings come along, and the old home parcel stays yours as land. Parcels can be claimed in any open sector, not just your home sector.

- **The grid.** 26×26 overall. The unlocked square grows 14 → 18 → 22 → 26.
  - **Expansion costs:** 8k cash and 60 power, then 40k and 250 power, then 120k and 600 power.
  - **Yard:** a 3-tile strip outside the walls where field buildings can go.
  - **Fog:** beyond the yard is fog. It's drawn as white low-poly clouds in the new graphics.
- **HQ.** 3×3 in the centre.
- **Building limit:** 4 + 3 × HQ level buildings, +2 with Town planning research.
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

- **Placement UI.** A ghost model appears at the nearest free spot by the HQ. Drag it or use the arrows, Rotate, then confirm. Green means it fits, red means blocked.
- **Defence:**
  - **Free garrison** sized by HQ level and barracks.
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

- **Contracts.** Sectors post contracts for goods; there are 6 open at a time.
- **Ship.**
  - **Legal road:** pays the nation's delivery tax (8% default) and the Caretaker's 5% fee.
  - **Back road:** untaxed and 1.3× slower. It risks the Caretaker checkpoint: a 35% base chance, scaled by standing, drones over the destination, Back channels research and sector presence.
  - **Bribes:** a stopped truck can be bribed through by raising the bribe.
  - **Audits:** a 35% chance, with a 1,800 fine.
- **Inspection.**
  - **Costs and limits:** an inspection costs 150, up to 20 a day per player.
  - **Wrong calls:** searching a clean truck costs compensation. The first two cost 300 each, then every miss in a row doubles it.
  - **Envelopes:** smugglers can leave an envelope (a bribe to the inspector), which the inspector takes or refuses.
- **Trucks** use the car-kit models on the maps.

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
  - **Starting troops:** 24 gunners, 10 launchers, 6 tanks, 3 helis, 30 militia, 3 strikes.
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

**Graphics:**
- **"Graphics: New"** (the default), toggled from the welcome card, applies to Sector, Province, Home and battles. It's a diorama look:
  - Sky gradient with light haze, no tone mapping, warm sun with soft shadows.
  - A colour grade (saturation 1.14, warmth, slight contrast, vignette) and a light tilt-shift.
  - Smooth vertex-coloured terrain with grass, sand and rock detail textures splatted by land type, darker hollows and rocky slopes.
  - Depth-shaded water with glints and foam.
  - Code-built low-poly pines, oaks, bushes, rocks, swaying grass and flowers, and cloud puffs.
  - **Battles sit in the real land.** Lane battles and base assaults are fought on a levelled clearing (rounded, with an uneven edge) cut into the nation map around where the fight is: the parcel you tapped, or a fixed dry spot in the sector. The land, trees, ruins, crops and water around the field are the same as on the sector map. The lanes are worn dirt paths, and a defender's yard is an irregular patch of trampled earth ringed by loose sandbags. Without the nation map (offline, or Classic), the old floating field is used.
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
| Players | **Player** (resources, standing, HQ level, home sector, tutorial and guide step, techs, research, alliance and role, nation, defence doctrine and trained defenders, raid timestamps, token hash plus `ExtraTokens`, recovery hash, `IsBot`), **HomeTile** |
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

**Rules for anyone working on this:**
- **Secrets:** never commit or print secrets (the admin key; any third-party keys are read from env vars only).
- **Destructive operations:** wipes and account resets delete real players' progress. Only do them when the owner asks.
- **Code style:** match the surrounding code. Short doc comments explain the *why*, and game text is written plainly, in British-leaning spelling ("defence", "armour").
