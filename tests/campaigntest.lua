-- Offline-Test mod\DcsCampaign.lua mit nachgebauter DCS-API:
-- "C:\Program Files\Eagle Dynamics\DCS World\bin\luae.exe" tests\campaigntest.lua mod\DcsCampaign.lua
local T, queue, out, handlers = 0, {}, {}, {}
env = { info = function(s) out[#out + 1] = s end, error = function(s) print("ENV ERROR " .. s) end }
timer = { getTime = function() return T end, getAbsTime = function() return 25200 + T end,
          scheduleFunction = function(f, a, t) queue[#queue + 1] = { f = f, a = a, t = t } end }
world = { event = { S_EVENT_SHOT = 1, S_EVENT_TAKEOFF = 3, S_EVENT_LAND = 4, S_EVENT_CRASH = 5, S_EVENT_EJECTION = 6, S_EVENT_DEAD = 8,
                    S_EVENT_PILOT_DEAD = 9, S_EVENT_MISSION_END = 12, S_EVENT_KILL = 28, S_EVENT_UNIT_LOST = 30, S_EVENT_BIRTH = 15, S_EVENT_PLAYER_LEAVE_UNIT = 21 },
          addEventHandler = function(h) handlers[#handlers + 1] = h end }
coalition = { side = { RED = 1, BLUE = 2 }, getCountryCoalition = function(c) return c == 0 and 1 or 2 end }
AI = { Option = { Ground = { id = { ALARM_STATE = 9 }, val = { ALARM_STATE = { AUTO = 0, GREEN = 1, RED = 2 } } } } }
local alarms = {}
coalition.getPlayers = function() return {} end           -- Einzelspieler-Weg: Spieler über DCSCAMP.player
Group = { Category = { AIRPLANE = 0 } }
local function obj(n, x, z)
  return { getName = function() return n end, getPoint = function() return { x = x, y = 5000, z = z } end,
           isExist = function() return true end, getLife = function() return 1 end }
end
local me = { x = 100, z = 200 }                                       -- Spieler: Position im Test verschiebbar
local lead = { getName = function() return "F12-1" end, getPoint = function() return { x = me.x, y = 5000, z = me.z } end,
               isExist = function() return true end, getLife = function() return 1 end, inAir = function() return true end }
Unit = { getByName = function(n) return n == "F12-1" and lead or nil end }
local bandit = { isExist = function() return true end, getLife = function() return 1 end, getPoint = function() return { x = me.x + 92600, y = 6096, z = me.z } end }
coalition.getGroups = function(side)
  if side == 2 then return { { getName = function() return "F12 Enfield 1" end, getUnits = function() return { lead } end } } end
  return { { getName = function() return "Fremd" end, getUnits = function() return {} end }, { getName = function() return "Bandit" end, getUnits = function() return { bandit } end } }
end
local function pt(x, z, eta, alt, tasks) return { x = x, y = z, alt = alt or 0, ETA = eta, type = "Turning Point", action = "Turning Point", task = { id = "ComboTask", params = { tasks = tasks or {} } } } end
local function units(pre, n, x, z) local u = {} for i = 1, n do u[i] = { name = pre .. "-" .. i, type = "T", x = x + i * 10, y = z } end return u end
DCSCAMP = { token = "tok", bridges = { { x = 500, z = 600 } }, player = "F12-1", max = 400,
            trains = { { g = "C7 Zug", id = 7, n = 4, c = 2, tx = 9000, tz = 9000 }, { g = "C8 Zug", id = 8, n = 2, c = 0, tx = 60000, tz = 60000 } },
            spawn = {
              -- Flug: startet t=100 bei (0,0), fliegt bis t=1100 nach (100 km, 0), dann Landung; Dauerauftrag CAP am ersten Punkt
              { n = "F20 Colt 1", k = "F20", c = 2, cat = 0, r = 185200, dep = "U6",
                g = { name = "F20 Colt 1", route = { points = { pt(0, 0, 100, 6000, { { id = "EngageTargets" } }), pt(100000, 0, 1100, 6000), pt(150000, 0, 2100, 0) } }, units = units("F20", 2, 0, 0) } },
              -- Bataillon steht 300 km entfernt: erst in der Bubble, wenn der Spieler hinfliegt
              { n = "U5 Btl", k = "U5", c = 0, cat = 2, r = 74080, g = { name = "U5 Btl", route = { points = { pt(300000, 0, 0) } }, units = units("U5", 3, 300000, 0) } },
              -- Angriffsziel von F20 (weit weg): kommt mit dem Angreifer, auch außerhalb der Bubble
              { n = "U6 Ziel", k = "U6", c = 0, cat = 2, r = 74080, g = { name = "U6 Ziel", route = { points = { pt(0, 500000, 0) } }, units = units("U6", 2, 0, 500000) } },
              -- Konvoi fährt t=0..1000 von (400 km, 0) nach (400 km, 100 km): erscheint später an der Planposition
              { n = "C9 Konvoi", k = "C9", c = 2, cat = 2, r = 74080, g = { name = "C9 Konvoi", route = { points = { pt(400000, 0, 0), pt(400000, 100000, 1000) } }, units = units("C9", 2, 400000, 0) } },
              -- Güterzüge am Start: 7 findet ein Gleis, 8 nicht
              { n = "C7 Zug", k = "C7", c = 2, cat = 2, r = 74080, g = { name = "C7 Zug", route = { points = { pt(1000, 1000, 0) } }, units = units("C7", 4, 1000, 1000) } },
              { n = "C8 Zug", k = "C8", c = 0, cat = 2, r = 74080, g = { name = "C8 Zug", route = { points = { pt(50000, 50000, 0) } }, units = units("C8", 2, 50000, 50000) } },
              -- SAM-Batterie 30 km vor dem Spieler, Netz = Frühwarnradar O41 (nicht in der Mission)
              { n = "O40 SAM", k = "O40", c = 0, cat = 2, r = 74080, iads = 40000, net = { "O41" }, g = { name = "O40 SAM", route = { points = { pt(30000, 0, 0) } }, units = units("O40", 2, 30000, 0) } },
              -- Artillerie nahe dem Start: schießt auf die feindliche Front
              { n = "U11 Art", k = "U11", c = 2, cat = 2, r = 74080, fire = { x = 15000, z = 0 }, g = { name = "U11 Art", route = { points = { pt(1000, 0, 0) } }, units = units("U11", 3, 1000, 0) } },
            },
            side = 2, awacs = "Overlord", smoke = { { x = 1, z = 2, p = 1 } }, craters = { { x = 3, z = 4 } }, wh = { [22] = { { 4, 4, 7, 24 } } } }
world.getAirbases = function()
  local wh = { getLiquidAmount = function() return 41500 end, getItemCount = function(_, t) return t[4] == 24 and 7 or 0 end }
  return { { getID = function() return 22 end, getWarehouse = function() return wh end }, { getID = function() return 5 end } }
end
local texts, smokes, pushed = {}, 0, {}
Object = { Category = { SCENERY = 5 } }
world.VolumeType = { SPHERE = 2 }
-- Gleis nur nahe (0..20000): Zug 7 findet eins, Zug 8 nicht. addGroup kennt für Züge nur Format B (Lok + Wagen).
land = { getHeight = function() return 10 end, isVisible = function() return true end, getClosestPointOnRoads = function(kind, x, z) if kind == "railroads" and x < 20000 then return x + 200, z end return 0, 0 end }
local groups, destroyed, added = {}, {}, {}
coalition.addGroup = function(c, cat, g)
  if cat == 4 and g.units[1].type ~= "Locomotive" then return end
  groups[g.name] = g added[#added + 1] = g.name
end
Group.getByName = function(n)
  local g = groups[n]
  if not g then return nil end
  local us = {}
  for _, u in ipairs(g.units) do us[#us + 1] = { isExist = function() return true end, getLife = function() return 1 end, getPoint = function() return { x = u.x, y = 0, z = u.y } end } end
  return { isExist = function() return true end, getSize = function() return #g.units end, getUnits = function() return us end,
           destroy = function() destroyed[n] = (destroyed[n] or 0) + 1 groups[n] = nil end,
           getController = function() return { pushTask = function(_, task) pushed[#pushed + 1] = { n = n, task = task } end, setOption = function(_, id, v) if id == 9 then alarms[n] = v end end } end }
end
local booms = 0
trigger = { action = { explosion = function() booms = booms + 1 end, effectSmokeBig = function() smokes = smokes + 1 end,
                       outTextForCoalition = function(side, s) texts[#texts + 1] = side .. ":" .. s end } }
world.searchObjects = function(cat, vol, f)
  for _, t in ipairs({ "house_1", "Bridge_Rail_1" }) do f({ getTypeName = function() return t end, getPoint = function() return vol.params.point end }) end
end
dofile(arg[1])

local function run(to)
  while true do
    table.sort(queue, function(a, b) return a.t < b.t end)
    local q = queue[1]
    if not q or q.t > to then break end
    table.remove(queue, 1)
    T = q.t
    local r = q.f(q.a, T)
    if r then queue[#queue + 1] = { f = q.f, a = q.a, t = r } end
  end
  T = to
end
local function fire(e) for _, h in ipairs(handlers) do h:onEvent(e) end end

local alive = true
local bomb = { isExist = function() return alive end, getPoint = function() return { x = 1000, y = 0, z = 2000 } end,
               getDesc = function() return { category = 3, typeName = "Mk_84", warhead = { explosiveMass = 429 } } end }
local aam = { isExist = function() return true end, getPoint = function() return { x = 0, y = 0, z = 0 } end,
              getDesc = function() return { category = 1, missileCategory = 1, typeName = "AIM_120" } end }
local fail = 0
local function ok(c, m) if not c then print("FEHLER: " .. m) fail = fail + 1 end end
math.random = function(a) return a or 0.1 end                          -- IADS würfelt: immer abschalten
run(10)
ok(groups["C7 Rail"] and #groups["C7 Rail"].units == 5 and destroyed["C7 Zug"], "Zug 7: Lok + 4 Wagen statt Lkw")
ok(groups["C8 Zug"] and not groups["C8 Rail"], "Zug 8 ohne Gleis bleibt Lkw-Kolonne")
ok(not groups["F20 Colt 1"] and not groups["U5 Btl"] and not groups["C9 Konvoi"], "nichts vor der Fahrplanzeit / außerhalb der Bubble")
fire({ id = 1, initiator = lead, weapon = bomb })
fire({ id = 1, initiator = lead, weapon = aam })
run(11) alive = false run(12)
fire({ id = 28, initiator = lead, target = obj("O5-1-r", 0, 0), weapon_name = "AGM_88" })
fire({ id = 8, initiator = obj("O5-1-r", 300, 400) })
fire({ id = 8, initiator = { getName = function() error("weg") end } })     -- Fehler im Objekt darf nichts kaputt machen
fire({ id = 6, initiator = lead })
fire({ id = 4, initiator = lead, place = obj("Kutaisi", 0, 0) })
run(70)
run(120)
ok(groups["F20 Colt 1"] and groups["F20 Colt 1"].route.points[1].x == 0, "Flug erscheint zur Startzeit am ersten Wegpunkt")
ok(groups["U6 Ziel"], "Angriffsziel erscheint mit dem Angreifer, auch außerhalb der Bubble")
-- Spieler fliegt zum Bataillon und zum Konvoi
me.x, me.z = 260000, 0 run(130)
ok(groups["U5 Btl"] and #groups["U5 Btl"].units == 3, "Bataillon erscheint, wenn der Spieler nah ist")
fire({ id = 8, initiator = obj("U5-2", 300000, 0) })
me.x, me.z = 400000, 60000 run(500)
local c9 = groups["C9 Konvoi"]
ok(c9 and #c9.route.points == 2 and math.abs(c9.units[1].y - 13400) < 100 and c9.route.points[1].ETA == 134, "Konvoi erscheint später an der Planposition (t=134: 13,4 km gefahren)"
   .. (c9 and string.format(" (z=%.0f, %d Punkte)", c9.units[1].y, #c9.route.points) or ""))
ok(not groups["U5 Btl"] and destroyed["U5 Btl"], "Bataillon beim Verlassen der Bubble entfernt")
ok(groups["U6 Ziel"], "Angriffsziel bleibt, solange der Angreifer unterwegs ist")
me.x, me.z = 260000, 0 run(560)
ok(groups["U5 Btl"] and #groups["U5 Btl"].units == 2, "Bataillon wieder da – ohne das zerstörte Fahrzeug")
me.x, me.z = 100, 200 run(1300)
local f20 = groups["F20 Colt 1"]
ok(f20 and math.abs(f20.route.points[1].x - 46400) < 100 and #f20.route.points == 3 and #f20.route.points[1].task.params.tasks == 1,
   "Flug nach Rückkehr in die Bubble an der Planposition, CAP-Auftrag bleibt")
groups["F20 Colt 1"] = nil run(1400)
run(2300)
ok(not groups["F20 Colt 1"], "gelandeter Flug kommt nicht wieder")
-- Live-Befehle über den Hook: neuer Konvoi nahe dem Spieler, dann wieder entfernen
DCSCAMP.cmd({ seq = 3, add = { { n = "C30 Konvoi", k = "C30", c = 2, cat = 2, r = 74080, g = { name = "C30 Konvoi", route = { points = { pt(5000, 0, 0) } }, units = units("C30", 2, 5000, 0) } } }, del = {}, msg = { "Lage 10:00: Gori eingenommen" } })
run(2320)
ok(groups["C30 Konvoi"], "Live-Befehl: neuer Konvoi erscheint in der Bubble")
DCSCAMP.cmd({ seq = 4, add = {}, del = { "C30" } })
run(2340)
ok(not groups["C30 Konvoi"] and destroyed["C30 Konvoi"], "Live-Befehl: Konvoi entfernt und kommt nicht wieder")
ok(alarms["O40 SAM"] == 2, "IADS: Batterie geht auf Sendung, Ziel in Reichweite und Netz lebt")
fire({ id = 1, initiator = lead, weapon = { isExist = function() return true end, getPoint = function() return { x = 20000, y = 3000, z = 0 } end,
                                            getDesc = function() return { category = 1, missileCategory = 6, guidance = 5, typeName = "AGM_88" } end } })
run(2346)
ok(alarms["O40 SAM"] == 1, "IADS: HARM im Anflug – Radar aus")
fire({ id = 8, initiator = obj("O41-1", 0, 90000) })
run(2351)
run(2500)
ok(alarms["O40 SAM"] == 2, "IADS: Netz weg, autonom wieder auf Sendung")
groups["F30 Jam"] = { units = { { x = 60000, y = 0 } } }               -- Störer 30 km hinter der Batterie, Spieler 30 km davor
DCSCAMP.jam = { { n = "F30 Jam", s = 2 } }
run(2510)
ok(alarms["O40 SAM"] == 1, "IADS: Störer – Ziel außerhalb Burn-through (16 km), Radar aus")
me.x, me.z = 20000, 0 run(2520)
ok(alarms["O40 SAM"] == 2, "IADS: Ziel innerhalb Burn-through – Radar an")
groups["F30 Jam"] = nil me.x, me.z = 100, 200 run(2530)
-- Mehrspieler: Mensch verlässt F12-2 in der Luft → KI-Ersatz fliegt heim, Platz zum Wieder-Einsteigen, Sperren
DCSCAMP.mp, DCSCAMP.homes = true, { F12 = { ad = 22, x = 7000, z = 8000, c = 2 } }
local function plane(n, inair, player)
  return { getName = function() return n end, getPoint = function() return { x = 1000, y = 4000, z = 2000 } end, getVelocity = function() return { x = 200, y = 0, z = 0 } end,
           inAir = function() return inair end, getTypeName = function() return "FA-18C_hornet" end, getFuel = function() return 0.5 end,
           getDesc = function() return { fuelMassMax = 4900 } end, getPlayerName = function() return player end, isExist = function() return true end, getLife = function() return 1 end }
end
local u2 = plane("F12-2", true, "Max")
fire({ id = 21, initiator = u2 })
fire({ id = 8, initiator = u2 })                                      -- DCS entfernt die verlassene Maschine: kein Verlust
local k = groups["F12-2K"]
ok(k and k.units[1].skill == "High" and k.route.points[2].airdromeId == 22 and k.units[1].payload.fuel == 2450, "KI-Ersatz fliegt mit Restsprit zum Heimatplatz")
ok(DCSCAMP.blocked["F12-2"] and not DCSCAMP.blocked["F12-2U"], "alter Platz gesperrt, Wieder-Einstieg offen")
fire({ id = 21, initiator = plane("F12-3", false, "Moritz") })
ok(not groups["F12-3K"] and not DCSCAMP.blocked["F12-3"], "am Boden verlassen: Platz bleibt frei")
fire({ id = 6, initiator = plane("F12-4", true, "Paul") })
fire({ id = 21, initiator = plane("F12-4", true, "Paul") })
ok(not groups["F12-4K"], "ausgestiegen: kein KI-Ersatz")
Unit.getByName = function(n) return groups[n] and plane(n, true) or (n == "F12-1" and lead or nil) end
run(T + 31)
local slot = groups["F12-2U"]
ok(slot and slot.units[1].skill == "Client" and slot.units[1].alt == 4150, "Platz zum Wieder-Einsteigen an der KI-Position")
fire({ id = 8, initiator = plane("F12-2U", true) })                   -- leerer Platz wird ersetzt: kein Verlust
fire({ id = 15, initiator = plane("F12-2U", true, "Max") })
ok(not groups["F12-2K"] and destroyed["F12-2K"], "Spieler übernimmt: KI-Ersatz weg")
fire({ id = 8, initiator = plane("F12-2K", true) })                   -- Entfernen des Ersatzes: kein Verlust
fire({ id = 4, initiator = plane("F12-2U", false, "Max"), place = obj("Kutaisi", 0, 0) })
ok(DCSCAMP.blocked["F12-2U"], "nach der Landung gesperrt")
fire({ id = 12 })

local s = table.concat(out, "\n")
print(s)
local function want(p) if not s:find(p, 1, true) then print("FEHLT: " .. p) fail = fail + 1 end end
want("DCSCAMP|tok|START|25200")
want("|SAM|27545|O40|harm") want("|SAM|27550|O40|auto") want("|O40|jam") want("|O40|clear")
want("DCSCAMP|tok|IMPACT|25211|Mk_84|1000|2000|429.0")
want("DCSCAMP|tok|KILL|25212|F12-1|O5-1-r|AGM_88")
want("DCSCAMP|tok|DEAD|25212|O5-1-r|300|400")
want("DCSCAMP|tok|EJECT|25212|F12-1")
want("DCSCAMP|tok|LAND|25212|F12-1|Kutaisi")
want("DCSCAMP|tok|T|25205")
want("DCSCAMP|tok|POS|25265|F12 Enfield 1|100|200|5000")
want("DCSCAMP|tok|END|") want("|WH|25800|22|41500|4,4,7,24=7") want("|WH|27761|22|41500|4,4,7,24=7")
want("DCSCAMP|tok|BRIDGE|25202|500|600|1|")
want("DCSCAMP|tok|SPAWN|25204|C7 Zug|4")
want("DCSCAMP|tok|TRAIN|25204|C7 Zug|ok|B")
want("DCSCAMP|tok|TRAIN|25204|C8 Zug|no track|\n")
want("DCSCAMP|tok|SPAWN|25304|F20 Colt 1|2")
want("DCSCAMP|tok|SPAWN|25304|U6 Ziel|2")
want("DCSCAMP|tok|OUT|")
want("|U5 Btl|3|")
want("|POS|")
want("|C7 Rail|")
want("DCSCAMP|tok|CMD|27500|3|1|0")
want("DCSCAMP|tok|CMD|27520|4|0|1")
ok(booms == 2, "Brücke + Pistenkrater: " .. booms .. " Explosionen statt 2")
ok(smokes == 1, "Rauch über dem Wrack")
ok(pushed[1] and pushed[1].n == "U11 Art" and pushed[1].task.id == "FireAtPoint" and pushed[1].task.params.point.x == 15000, "Artillerie feuert auf die feindliche Front")
local tx = table.concat(texts, "\n")
ok(tx:find("2:Overlord: picture, 1 group hostile. Nearest BRA 000 / 50 NM / 20000 ft.", 1, true), "AWACS-Lagebild: " .. tx)
ok(tx:find("2:Lage 10:00: Gori eingenommen", 1, true), "Lagemeldung über den Hook als Funktext")
ok(not s:find("AIM_120", 1, true), "Luft-Luft-Rakete darf nicht als Einschlag gemeldet werden")
ok(not s:find("Fremd", 1, true), "fremde Gruppe gemeldet")
ok(not s:find("DEAD|%d+|U5%-1"), "Entfernen beim Verlassen der Bubble ist kein Verlust")
ok(not s:find("DEAD|%d+|F12%-2|"), "Mehrspieler: Ersetzen und Wieder-Einsteigen ist kein Verlust")
want("|AIHOME|") want("|F12-2|ok") want("|BIRTH|") want("|F12-2|Max") want("|F12-2|Kutaisi")
print(fail == 0 and "ALLES OK" or (fail .. " FEHLER"))
os.exit(fail == 0 and 0 or 1)
