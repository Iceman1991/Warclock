Warclock – real-time dynamic campaign for DCS World (Caucasus, Persian Gulf)
============================================================================

Start: Start menu → Warclock. A small (minimized) window keeps the campaign running,
the interface opens in your browser: http://localhost:8642/
Closing the window = pausing the campaign (progress is saved, also automatically every minute).
Main menu (☰ top right): Continue, New campaign (theater, side, fog of war, aircraft carrier, seed), Load save.
Aircraft carrier (selectable when starting a campaign): Supercarrier CVN-75 Truman (DCS module required), Stennis (no module) or
no carrier. With a carrier, the F/A-18C and F-14B take off and land on the ship (Black Sea west of Batumi or Gulf
north of Abu Dhabi); it steams back and forth into the wind. TACAN 75X TRU or 74X STN, ICLS 11, radio 127.5 AM,
Supercarrier additionally Link 4 336.0 and ACLS.
Splash Damage (recommended, not included): download Splash_Damage_*.lua by Stevey666 from the DCS User Files
(https://www.digitalcombatsimulator.com/en/files/3344761/) and put it into %LOCALAPPDATA%\DCS-Kampagne\scripts. With the setup option ticked,
the campaign builds the newest version there into every mission (if it is missing, the event log says so). Unchecked: missions without it.
The running war goes into the archive (saves\kampagne-date.json) and can be loaded again.
Theaters (the map must be installed in DCS, otherwise greyed out):
 - Caucasus: Russia attacks Georgia, NATO helps from the air. Five axes, rail, road, sea.
 - Gulf: Landing in the Northern Emirates – Iran holds Musandam and Ras al-Khaimah, land front toward Sharjah,
   Al Ain and Fujairah. Iranian supply only by ship from Bandar Abbas (port destroyed = no freighters).
 - Gulf: Iran Outside Dubai – the same forces, the front is already outside Dubai, Al Madam and Fujairah.
 - Gulf: Tanker War – no land front: ports, refineries, island positions, freighters in the Strait of Hormuz.
   The war ends when one side has less than 25 % of its war economy (ports, plants, depots, HQs) left
   after at least 7 days.
 In the Gulf: Blue = Emirates, Oman, USA (playable F/A-18C, F-14B, F-15E); Red = Iran (F-14A, MiG-29A, F-4E, F-5E,
 Su-24, Su-25). The Iranian hinterland (Bandar Abbas, Qeshm, islands, Lar, Jask) is a target for air strikes, without a road network.
 Iran fires missiles and drones (Missile Brigade Bastak, Drone Base Bandar Khamir) at ports, plants, depots and
 airfields; Patriots in range intercept some of them. Bombing the launch site = fewer or no salvos.
The bar on the left opens the windows (keys 1–7: Situation, ATO, Flight, Logistics, Squadrons, Reports, Debriefing);
windows can be dragged, resized (bottom right corner) and collapsed. Esc = clear selection.

How it works
 1. Let the clock run at the top (▶/❚❚ or space bar; time compression 1× … 1024×, "+1 h" skips ahead).
    Both sides plan missions, ground units attack, convoys haul supplies,
    air defenses and radars scout (fog of war).
 2. The "Flight" window guides you through planning in 5 steps (like Falcon BMS), always "Next" at the bottom:
    1 Tasking: a list of all flights you can fly, sorted by TOT – planned flights of all types with a
      DCS cockpit (F/A-18C, F-14, F-15E, F-16C, F-15C, A-10C, Su-25T, AH-64D, UH-1H, C-130J …) and new tasks ("new": creates a package, SEAD and escort
      if needed, takeoff in about 45 min). Filter by type and kind. Flights already airborne: "Join" (air start).
      Filters: type, planned/new, task (also groups), airfield, takeoff time, sorting (TOT, distance, type),
      search (target, callsign, airfield); ✕ resets everything. Hovering over a row shows the route on the map.
      The AI keeps two aircraft per playable squadron free for you. You can switch at any time.
 3. 2 Plan: type (rather an F-16 than the F-15E? – another squadron flies the same task, same TOT,
      route and weapons re-planned), start (cold/hot/runway/air), TOT, crew (alone or with AI wingmen, − / +),
      waypoints, check. 3 Weapons: a loadout or each station
      individually (your own loadouts from the DCS mission editor show up too). 4 Cartridge: chaff, flares,
      gun, fuel; expandable ALE-47 MAN 1–6, 20 named radio channels per radio, TACAN, altitude warning, helmet.
      Everything on one page; profiles store the equipment per type for all campaigns (the profile "Default"
      is applied to every new campaign). The equipment per type also applies to the next flights. 5 Start: summary.
      Multiplayer (co-op against the AI, only you plan): choose "Multiplayer" in step 5. In step 1,
      "+ add" puts more flights into the same mission (click = plan, ✕ = back to the AI), in step 2 choose
      player or AI per aircraft. In DCS: Multiplayer → New server → mission from the Kampagne folder; other players
      take the "Player" slots. If someone leaves while airborne, the AI flies the aircraft home; the slot "F…-…U" in
      the slot list takes it back. Landed, lost and AI-flown aircraft are locked.
      Once all players have landed, the campaign evaluates the mission; the server loads the next mission by itself.
      This requires choosing "Live connection" during installation (hook).
    F/A-18C: the cartridge (DTC) is generated from your template in Saved Games\DCS\DTC (named radio channels,
    ALE-47, altitude warning, TACAN, known SAM rings, front line). Set laser codes and datalink in the cockpit.
    Map: drag waypoints, Alt+click on the route = insert waypoint, right-click = delete.
    "Check" shows fuel (with a matching tanker: frequency/TACAN), known air defenses on the route,
    timing and conflicts with other friendly flights (< 3 km, < 300 m). Attackers automatically fly low in known
    threat zones (150 m above the terrain).
 4. "Generate mission and fly" (step 5): the mission is then in Saved Games\DCS\Missions\Kampagne
    (older campaign missions there are deleted).
 5. In DCS: Mission → Kampagne folder → open the file and fly. Kneeboard: situation report + tasking + map.
    The war clock waits for DCS and then runs with DCS time. The mission contains the schedule of the
    next 8 hours: AI flights, ground units, SAMs, convoys, trains and freighters only appear when they
    come near you (air 100 NM, ground 40 NM), and disappear again when you move away – so you
    can fly for several hours. Artillery fires at the front, wrecks stay where they are, AWACS reports
    the air picture as radio text every 5 minutes.
 6. End the mission in DCS: hits, losses, kills, landings and ejections are
    taken over (the campaign reads dcs.log). Results in the "Debriefing" window.
    After landing: "Flight" window → "Next flight" generates the next mission, the war continues seamlessly.
    Only if DCS crashes: "End mission" at the top (if DCS restarts, the running mission ends by itself).
    Optional (setup task "Live connection"): a hook in Saved Games\DCS\Scripts\Hooks passes new flights,
    convoys and situation reports to DCS every minute during the mission. Without the hook only the schedule applies.
    In multiplayer it locks slots and loads the next mission on the server.

Map: yellow line = front between red and blue territory, bar bottom left = zoom / whole map,
hovering over a symbol shows its name. "Layers" (bar on the left, key E): air defense and radar for the altitude
on the slider, supply routes (unit → depot, color = supply state, convoy routes), the "Time" slider shows your
own flights up to 4 hours ahead as planned, plus a legend of the symbols.
Alert flights (QRA) launch on their own when the enemy shows up near an airfield. The war ends (banner) when one side
loses all key locations or all airfields, or has only 20 % of the enemy's ground strength left.

Commander: click a target on the map → choose the mission type → "Create package" (also with a playable
squadron, then take over the flight under "Flight"). "Situation" window: phase of the air war (air superiority, SEAD,
interdiction, ground offensive) – the AI chooses it by itself according to the situation, you can set it.
Targeting priorities (like in BMS): off / normal / high per target type and a main-effort axis – targets up to
60 km around its front count one and a half times. Applies to the AI planning of your side and the order of the task list.

Pre-war intel: the peacetime positions of enemy SAMs are known at the start of the war (knowledge of
positions lasts 48 h, of mobile units 12 h). Strikes route around known air defenses, the AI plans SEAD for them or
drops the strike. The task list warns "⚠ under the umbrella of … – SEAD needed"; a new package without available
SEAD reports "⚠ air defense on the route". CAP, AWACS and tanker stations stay out of known SAM rings.
At the start of the war, air defenses stand behind the front – no SAM ring reaches across the border.
Planning as in reality: SAMs from the outside in (those near the front first, a site under the umbrella of a forward one only
once that one has fallen), early-warning radars first on day one. Routes also avoid known fighter patrols and
air bases; if there are fighters on the route, a fighter sweep flies 8 min ahead of the package – the task list warns
"⚠ fighters on the route". CAS also on call over the line of contact and against enemy staging.
CAS waits on the friendly side of the front (racetrack parallel to the front) and attacks on call across
the line of contact; the SEAD flight stays for the whole time on station.
DCS AI on a leash: AI attackers and SEAD only go for the tasked target (no dogfights, no emergency jettison, evade
instead of aborting), fighters without ground targets, CAP chases at most 75 km away from its station, bingo = home.
Ground units and convoys do not disperse (otherwise they get stuck in the terrain), SAMs with radar on.
The war reacts: planned strikes on targets that are already destroyed are cancelled, in bad weather
no new strikes launch. Spotted SAM sites relocate – a strike on the old position hits
nothing. Without early-warning radar/AWACS, SAMs light up late, and before SEAD flights they go dark at times;
HARM hits only knock out the radar (replaced after about 10 h), only a bomb strike destroys a site for good (DEAD).
Weather fronts move through from west to east (top: distance and arrival). Engineers rebuild destroyed bridges
when a friendly depot nearby has material.

Supply ("Logistics" window, map: "Railways" layer): plants send their production as freight trains over the
rail network (coastal railway Sochi–Sukhumi–Zugdidi, Transcaucasian railway Senaki–Kutaisi–Tbilisi, lines to
Poti/Batumi, North Caucasus Mineralnye Vody–Prokhladny–Vladikavkaz) to the nearest depot with a rail link.
If the line is cut (bridge destroyed, station captured), a smaller truck convoy takes the
road. Every morning a freighter (600 t) arrives from the sea in Poti, Batumi and Sochi – freighters can only be
spotted by coastal radar/AWACS. Truck convoys carry the material on to forward depots and airfields.
Bridges, trains and freighters are worthwhile targets.
Supply only enters through entry points at the map edge (Red: rail from the north, Roki Tunnel, Darial; Blue:
ports, Sarpi, Red Bridge). Ammunition and fuel are tracked separately; units have their own stock and a
supply line to the forward depot. No ammunition, no attack; no fuel, no movement; units that are cut off
surrender. Whoever hits the enemy's bridges and trains stops his offensive – and vice versa.

War in waves: per axis staging (gathering stock and units – recon reports it) → attack →
culmination → consolidation; the defender counters with reserves. Less happens at night and in snow.
Situation report every morning at 06:00 ("Situation" window and kneeboard): front, losses, logistics, presumed intent of the
enemy. Enemy reports carry age and certainty (confirmed < 3 h, probable < 12 h, otherwise suspected),
strength estimates have errors. Some SAM sites are decoys – reconnaissance flights expose them.
Helicopters: Apache (Blue, playable), Mi-24P (Red, playable), AH-1W in the Gulf; they fly CAS and BAI at low level
from a firing position on the friendly side of the front, without SEAD/fighter cover, and are assigned to targets near the front first.
Pilots shot down over enemy territory: the rescue squadron (UH-1H, Mi-8) flies a CSAR mission to the pilot –
if it gets through, he is rescued; air defenses can also get the rescuers. After 4 h without rescue: captivity.
Flying CSAR yourself: take over the planned CSAR flight of the UH-1H (Huey module), F10 → Campaign → Situation names the
pilot with coordinates and a marker, within 10 km he pops red smoke; landing within 500 m of him picks him up.
Airlift: C-130J (Blue, playable), C-17, Il-76, An-26, and in the Gulf also Iranian C-130s fly ammunition and fuel
from their home base to the friendly airfield that is shortest of supplies and land there – the cargo counts when it arrives (transports
shot down lose their load). Fly it yourself: take over a planned C-130J transport flight and land at the destination airfield.
Runway craters close airfields until engineers repair them with depot material.
Performance: DCS only gets what is close to the player – ground units and helicopters from 30 NM, AI aircraft from 60 NM,
enemy fighters, your own package and AWACS/tankers from 100 NM; nearest first. Upper limit at the same time: step 5
"Performance" – weak PC 150 vehicles / 24 aircraft, normal 300 / 40, strong PC 500 / 64. If it stutters: "weak PC"
and turn off civilian traffic in DCS (game setting, the campaign does not force it).

In the cockpit: F10 → Campaign (requires the live hook)
 - Situation and tasking: your own task, latest front reports, known air defenses nearby, AWACS/tankers,
   nearest friendly pilot shot down (up to 100 NM).
 - CAS: check in with the JTAC: the JTAC at the nearest line of contact gives target, coordinates (degrees/minutes, MGRS, elevation),
   friendly troops, threats and egress direction, and marks with red smoke and a map marker.
 - New target (pop-up): up to four targets within 80 NM to choose from – the one you pick is what your flight flies (divert).
 - Next sortie (after landing at a friendly airfield): tasks from this airfield; the aircraft becomes a
   new flight with the same callsign, ground crew (F8) refuels and rearms, then off again.
   Multiplayer: once everyone has landed, the mission only ends after 5 minutes – time for "Next sortie".
 Each of these radio calls is also written to %TEMP%\DcsCampaign-Funk.txt – DCS-ATC can speak it.

Files
 - Save: %LOCALAPPDATA%\DCS-Kampagne\saves\kampagne.json (plus kampagne.json.bak)
 - Missions: Saved Games\DCS\Missions\Kampagne
 - No changes to DCS files, no admin rights, no graphics card required.
 - Live hook (only if chosen during installation): Saved Games\DCS\Scripts\Hooks\DcsCampaignHook.lua,
   commands via %TEMP%\DcsCampaign-Cmd.

DCS-ATC (setup option, tower, AWACS, radio): if chosen and missing or a newer release is out, the campaign downloads the public release on startup
(github.com/Iceman1991/DCS-ATC-Release, about 790 MB) and installs it; its setup asks once for admin rights.
Progress is shown in the header bar. "Fly" also starts DCS-ATC if it is not already running.
In campaign missions, ATC's extra traffic, ATC tankers/AWACS and random fog switch off – traffic,
tankers, AWACS and weather come from the campaign; the AWACS is voiced by DCS-ATC (no more text picture from the campaign).

Known limitations
 - Bridge hits are detected by the impact point (about 600 m radius).
 - Bridges that are already destroyed are blown up at mission start (search within 600 m; result as a
   BRIDGE line in dcs.log – not yet tested in DCS).
 - Radar layer: the altitude on the slider counts as height above ground.
 - Schedule, artillery, wrecks, craters and live hook: verified in the self-test, not yet tested in DCS.
 - The war plan reaches about 3 h ahead; what gets planned after that only reaches the running mission with the hook.
 - Freight trains: at mission start the mission puts a real train on the nearest track; if that fails, the
   truck convoy remains (TRAIN line in dcs.log and in the debriefing – not yet tested in DCS). Stations are at the
   towns, the lines between them are straight.
 - Freighters stop 4 km off the port (DCS ships do not run aground), in the Gulf further out depending on the coast.
 - Gulf: the campaign only knows parking spots after the first mission on that map (via DCS-ATC, which records them
   in every mission) – until then takeoff from the runway. Gulf scenarios not yet tested in DCS.
 - Gulf: coast and islands are approximated by polygons (the coastal plain is at 0 m in the height grid).
 - Multiplayer: verified in the self-test, not yet tested in DCS – especially AI + players in one flight, the
   "…U" slot for rejoining (test: F10 → "Test: create player slot here") and slot locking. The AI flies
   abandoned aircraft home without weapons. Kneeboard: all pages of all flights (with callsigns) for everyone.
