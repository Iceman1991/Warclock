-- DCS-Kampagne: meldet, was in der Kampagnenmission passiert, über dcs.log an DcsCampaign.exe (liest mit).
-- Nur env.info – keine Änderung an MissionScripting.lua nötig. Zeilen: DCSCAMP|token|art|absZeit|...
--   START / T (jede Minute) / POS gruppe x z höhe / DEAD name x z / KILL schütze ziel waffe / EJECT, PILOTDEAD, TAKEOFF name
--   LAND name platz / IMPACT waffe x z sprengstoff_kg / BRIDGE x z treffer typen / TRAIN gruppe ergebnis / END
--   SPAWN gruppe einheiten / OUT gruppe einheiten x z: Fahrplan-Gruppe in der Bubble um die Spieler erzeugt bzw. beim Verlassen entfernt
--   BIRTH / LEAVE einheit spieler: Mensch steigt ein bzw. verlässt die Maschine / TESTSLOT ergebnis (Mehrspieler-Test)
--   SAM schlüssel harm|auto: IADS – Batterie schaltet vor einem HARM ab bzw. hat ihre Frühwarnung verloren (autonom)
--   AIHOME einheit ok|fehler: Mehrspieler, Mensch hat die Maschine in der Luft verlassen – KI fliegt sie heim
--   MENU einheit frage [schlüssel]: F10 → Kampagne (lage, jtac, ziele, weiter, pick) – die App antwortet über den Hook (say/mark/smoke/menu)
-- Mehrspieler-Namen: "F12-2K" = KI-Ersatz, "F12-2U" = Platz zum Wieder-Einsteigen; gemeldet wird immer "F12-2".
do
  local D = DCSCAMP or {}
  local token = D.token or "?"
  local function f0(v) return string.format("%.0f", v) end
  local function log(kind, ...)
    local parts = { "DCSCAMP", token, kind, f0(timer.getAbsTime()) }
    for _, v in ipairs({ ... }) do parts[#parts + 1] = (tostring(v):gsub("|", "/")) end
    env.info(table.concat(parts, "|"))
  end
  local function raw(o)
    if not o then return nil end
    local ok, n = pcall(o.getName, o)
    return ok and n or nil
  end
  local function name(o) local n = raw(o) return n and (n:gsub("^(F%d+%-%d+)[KU]$", "%1")) end
  local function pos(o)
    local ok, p = pcall(o.getPoint, o)
    if ok and p then return f0(p.x), f0(p.z) end
    return "0", "0"
  end
  local function key(n) return n and n:match("^(%a%d+)") end
  local dead, quiet = {}, {}          -- tote Einheiten (nicht wieder erzeugen); Gruppen, die das Skript gerade selbst entfernt
  local gone, seated = {}, {}         -- Mehrspieler: Maschinen, die das Skript gerade ersetzt (kein Verlust); besetzte Plätze "F12-2U"
  D.blocked = {}                      -- Plätze, die nicht mehr wählbar sind (gelandet, verloren, KI fliegt) – liest DcsCampaignHook.lua
  local function block(n) D.blocked[n] = true D.blocked[n .. "U"] = true end
  local mp = {}                       -- Mehrspieler-Funktionen (unten)
  -- Platzlager (DCSCAMP.wh = { [Platz-ID] = { wsType, … } }): Rest Kerosin (kg) und Waffen melden, alle 10 min und zum Missionsende
  local function whlog()
    if not D.wh then return end
    for _, ab in ipairs(world.getAirbases() or {}) do
      pcall(function()
        local ws = D.wh[ab:getID()]
        if not ws then return end
        local wh, n = ab:getWarehouse(), {}
        for _, t in ipairs(ws) do n[#n + 1] = table.concat(t, ",") .. "=" .. wh:getItemCount(t) end
        log("WH", ab:getID(), f0(wh:getLiquidAmount(0)), table.concat(n, ";"))
      end)
    end
  end
  timer.scheduleFunction(function(_, t) whlog() return t + 600 end, nil, timer.getTime() + 600)

  -- Bomben, Raketen, Luft-Boden-Flugkörper verfolgen: letzter Punkt vor dem Verschwinden = Einschlag (Brücken, Pisten)
  local shots, harms, lost = {}, {}, {}   -- Einschläge; Radar-Flugkörper im Flug (IADS); zerstörte Gruppen-Schlüssel (Frühwarnnetz)
  local function track(wpn)
    local ok, d = pcall(wpn.getDesc, wpn)
    if not ok or not d then return end
    if d.guidance == 5 then harms[#harms + 1] = wpn end                       -- Weapon.GuidanceType.RADAR_PASSIVE: HARM & Co.
    local c, mc = d.category, d.missileCategory
    if c == 3 or c == 2 or (c == 1 and (mc == 4 or mc == 5 or mc == 6)) then
      shots[#shots + 1] = { w = wpn, t = d.typeName or "?", m = (d.warhead and d.warhead.explosiveMass) or 0 }
    end
  end
  timer.scheduleFunction(function(_, t)
    for i = #shots, 1, -1 do
      local s = shots[i]
      local ok, p = pcall(function() return s.w:isExist() and s.w:getPoint() end)
      if ok and p then s.p = p
      else
        if s.p then log("IMPACT", s.t, f0(s.p.x), f0(s.p.z), string.format("%.1f", s.m)) end
        table.remove(shots, i)
      end
    end
    return t + 0.2
  end, nil, timer.getTime() + 1)

  local E = world.event
  local H = {}
  function H:onEvent(e)
    local ok, err = pcall(function()
      local id, o = e.id, e.initiator
      if id == E.S_EVENT_DEAD or id == E.S_EVENT_CRASH or id == E.S_EVENT_UNIT_LOST then
        local n, r = name(o), raw(o)
        local skip = n and ((quiet[key(n)] and timer.getTime() - quiet[key(n)] < 5) or (gone[n] and timer.getTime() - gone[n] < 10)
          or (r:sub(-1) == "U" and not seated[r]))                                    -- leerer Platz zum Wieder-Einsteigen ist keine Maschine
        if n and not skip then dead[n] = true lost[key(n)] = true block(n) log("DEAD", n, pos(o)) end
      elseif id == E.S_EVENT_KILL then log("KILL", name(o) or "?", name(e.target) or "?", e.weapon_name or "?")
      elseif id == E.S_EVENT_EJECTION or id == E.S_EVENT_PILOT_DEAD then
        local n = name(o)
        if n then dead[n] = true end                                                -- Maschine ohne Pilot: kein KI-Ersatz
        log(id == E.S_EVENT_EJECTION and "EJECT" or "PILOTDEAD", n or "?")
      elseif id == E.S_EVENT_TAKEOFF then log("TAKEOFF", name(o) or "?")
      elseif id == E.S_EVENT_LAND then
        local p = o and o:getPoint()
        log("LAND", name(o) or "?", raw(e.place) or "", p and f0(p.x) or "", p and f0(p.z) or "")
        if name(o) and e.place then block(name(o)) end                              -- Hubschrauber im Gelände: Platz bleibt frei
      elseif id == E.S_EVENT_SHOT and e.weapon then track(e.weapon)
      elseif id == E.S_EVENT_MISSION_END then whlog() log("END")
      elseif o and (id == E.S_EVENT_BIRTH or id == E.S_EVENT_PLAYER_LEAVE_UNIT) then
        local okp, pn = pcall(o.getPlayerName, o)
        if okp and pn then
          log(id == E.S_EVENT_BIRTH and "BIRTH" or "LEAVE", name(o) or "?", pn)
          if D.mp and mp[id] then mp[id](o) end
        end
      end
    end)
    if not ok then env.error("DCSCAMP " .. tostring(err)) end
  end
  world.addEventHandler(H)

  -- Schon zerstörte Brücken (DCSCAMP.bridges) auch in DCS sprengen. Lage aus Kartendaten: Brückenobjekt im Umkreis von 600 m suchen.
  -- BRIDGE x z treffer [Objekttypen in der Nähe, wenn keine Brücke erkannt]
  timer.scheduleFunction(function()
    for _, b in ipairs(D.bridges or {}) do
      local p = { x = b.x, y = land.getHeight({ x = b.x, y = b.z }), z = b.z }
      local hit, seen = 0, {}
      world.searchObjects(Object.Category.SCENERY, { id = world.VolumeType.SPHERE, params = { point = p, radius = 600 } }, function(o)
        local ok, t = pcall(o.getTypeName, o)
        t = ok and t and t:lower() or ""
        if t:find("bridge") or t:find("^most") then trigger.action.explosion(o:getPoint(), 2000) hit = hit + 1
        elseif #seen < 8 then seen[#seen + 1] = t end
        return true
      end)
      log("BRIDGE", f0(b.x), f0(b.z), hit, hit == 0 and table.concat(seen, ",") or "")
    end
  end, nil, timer.getTime() + 2)

  -- Güterzug: echten Zug aufs nächste Gleis setzen und die Ersatz-Lkw entfernen; geht das nicht, bleiben die Lkw.
  -- Ergebnis "ok"|"kein Gleis"|"fehler" + Format: A = ein Zug mit Wagenliste, B = Lok + Wagen als Einheiten.
  local function train(t, x, z)
    local cat = Group.Category.TRAIN or 4
    local ax, az = land.getClosestPointOnRoads("railroads", x, z)
    local bx, bz = land.getClosestPointOnRoads("railroads", t.tx, t.tz)
    if not ax or not bx or (ax - x) ^ 2 + (az - z) ^ 2 > 3000 ^ 2 then return "no track" end
    local hdg = math.atan2(bz - az, bx - ax)
    local function pt(px, pz) return { x = px, y = pz, alt = 0, type = "On Railroads", action = "On Railroads", speed = 16.7 } end
    local cars = { "Locomotive" }
    for i = 1, t.n do cars[#cars + 1] = (i % 3 == 0) and "Coach a tank blue" or "Coach cargo" end
    local gname = "C" .. t.id .. " Rail"
    local forms = { A = { { type = "Train", name = "C" .. t.id .. "-1/1-x", x = ax, y = az, heading = hdg, skill = "Average", wagons = { wagons = cars } } }, B = {} }
    for i, c in ipairs(cars) do
      forms.B[i] = { type = c, name = "C" .. t.id .. "-" .. i .. "/" .. #cars .. "-x", x = ax - math.cos(hdg) * 25 * (i - 1), y = az - math.sin(hdg) * 25 * (i - 1), heading = hdg, skill = "Average" }
    end
    for _, f in ipairs({ "A", "B" }) do
      pcall(coalition.addGroup, t.c, cat, { name = gname, task = "Ground Nothing", route = { points = { pt(ax, az), pt(bx, bz) } }, units = forms[f] })
      local g = Group.getByName(gname)
      if g and g:isExist() and g:getSize() > 0 then
        local old = Group.getByName(t.g)
        if old then quiet[key(t.g)] = timer.getTime() old:destroy() end
        return "ok", f, gname
      end
    end
    return "error"
  end

  -- ---------- Fahrplan + Bubble ----------
  -- DCSCAMP.spawn: { n = Gruppe, k = "F12"/"U5"/"O7"/"C9", c = Land, cat = 0 Flugzeug / 2 Boden / 3 Schiff, r = Bubble-Radius m, dep = Ziel-Schlüssel, g = Gruppe wie im Editor }
  -- Planposition aus den Wegpunkt-ETAs. Erscheint eine Gruppe später als geplant, setzt das Skript sie dorthin und lässt passierte Punkte weg.
  local S = D.spawn or {}
  local byKey, trains = {}, {}
  for _, t in ipairs(D.trains or {}) do trains[t.g] = t end
  for _, e in ipairs(S) do byKey[e.k] = e e.train = e.train or trains[e.n] e.base = e.n end
  local function copy(t)
    if type(t) ~= "table" then return t end
    local r = {}
    for k, v in pairs(t) do r[k] = copy(v) end
    return r
  end

  -- x, z, Höhe, Index des letzten passierten Punkts; nil = noch nicht da, nil + true = Flug vorbei
  local function plan(e, t)
    local p = e.g.route.points
    if e.cat < 2 and t < (p[1].ETA or 0) then return nil end
    for i = 1, #p - 1 do
      local a, b = p[i], p[i + 1]
      if t < (b.ETA or 0) then
        local f = b.ETA > (a.ETA or 0) and math.max(0, (t - (a.ETA or 0)) / (b.ETA - (a.ETA or 0))) or 0
        return a.x + (b.x - a.x) * f, a.y + (b.y - a.y) * f, (a.alt or 0) + ((b.alt or 0) - (a.alt or 0)) * f, i
      end
    end
    if e.cat < 2 then return nil, nil, nil, nil, true end
    return p[#p].x, p[#p].y, 0, #p
  end

  local function spawn(e, t)
    local x, z, alt, i = plan(e, t)
    if not x then return end
    local g = copy(e.g)
    local p = g.route.points
    if t > (p[1].ETA or 0) + 30 then
      local np = copy(p[i])
      np.x, np.y, np.alt, np.ETA = x, z, alt, t
      if e.cat < 2 then
        np.type, np.action = "Turning Point", "Turning Point"
        local tasks = np.task.params.tasks                  -- Aufträge des passierten Punkts + Daueraufträge vom Start (CAP, Tanker, AWACS)
        if i > 1 then for _, tk in ipairs(p[1].task.params.tasks or {}) do tasks[#tasks + 1] = copy(tk) end end
        for k, tk in ipairs(tasks) do tk.number = k end
      end
      local pts = { np }
      for j = i + 1, #p do pts[#pts + 1] = p[j] end
      g.route.points = pts
      local nx = pts[2] or np
      local hdg = math.atan2(nx.y - z, nx.x - x)
      local ux, uy = g.units[1].x, g.units[1].y
      for _, u in ipairs(g.units) do
        u.x, u.y = x + u.x - ux, z + u.y - uy
        if e.cat < 2 then u.alt, u.heading, u.psi = alt, hdg, -hdg end
      end
      g.x, g.y = x, z
    end
    local alive = {}
    for _, u in ipairs(g.units) do if not dead[u.name] then alive[#alive + 1] = u end end
    if #alive == 0 then e.done = true return end
    g.units = alive
    local ok, err = pcall(coalition.addGroup, e.c, e.cat, g)
    if not ok or not Group.getByName(e.n) then env.error("DCSCAMP spawn " .. e.n .. ": " .. tostring(err)) e.done = true return end
    e.live, e.on = true, nil
    log("SPAWN", e.n, #alive)
    if e.train then
      local tok, res, form, gname = pcall(train, e.train, x, z)
      log("TRAIN", e.base, tok and res or "error", tok and (form or "") or tostring(res))
      if tok and gname then e.n = gname end
    end
    return #alive
  end

  local function out(e, t)
    local gr = Group.getByName(e.n)
    e.live = false
    if not gr or not gr:isExist() then e.done = true return end
    local n, px, pz = 0, "0", "0"
    for _, u in ipairs(gr:getUnits() or {}) do
      if u:isExist() and u:getLife() > 0 then n = n + 1 if n == 1 then px, pz = pos(u) end end
    end
    quiet[e.k] = t
    log("OUT", e.n, n, px, pz)
    gr:destroy()
    e.n = e.base
  end

  -- ---------- F10 „Kampagne“ je Spielergruppe: Fragen gehen als MENU an die App, Antworten kommen über den Hook ----------
  local menus, picks, marks, nextMark = {}, {}, {}, 7300
  local function gidOf(un) local u = Unit.getByName(un or "") return u and u:isExist() and u:getGroup():getID() end
  local function ask(a) log("MENU", a[1], a[2], a[3] or "") end
  local function kmenu(u)
    local okg, g = pcall(function() return u:getGroup():getID() end)
    local un = raw(u)
    if not okg or not un or menus[g] then return end
    local root = missionCommands.addSubMenuForGroup(g, "Campaign")
    menus[g] = root
    missionCommands.addCommandForGroup(g, "Situation and tasking", root, ask, { un, "lage" })
    missionCommands.addCommandForGroup(g, "CAS: check in with JTAC", root, ask, { un, "jtac" })
    missionCommands.addCommandForGroup(g, "New target (pop-up)", root, ask, { un, "ziele" })
    missionCommands.addCommandForGroup(g, "Next sortie (after landing)", root, ask, { un, "weiter" })
  end
  local function where(x, z)                                -- Koordinaten wie im Kneeboard: Grad/Minuten, MGRS, Höhe
    local h = land.getHeight({ x = x, y = z })
    local lat, lon = coord.LOtoLL({ x = x, y = h, z = z })
    local function dm(v, w, p, n) local a = math.abs(v) local d = math.floor(a) return string.format("%s %0" .. w .. "d°%06.3f'", v >= 0 and p or n, d, (a - d) * 60) end
    local s = dm(lat, 2, "N", "S") .. " " .. dm(lon, 3, "E", "W")
    local ok, m = pcall(coord.LLtoMGRS, lat, lon)
    if ok and m then s = s .. string.format(" · MGRS %s %s %05d %05d", m.UTMZone, m.MGRSDigraph, m.Easting, m.Northing) end
    return s .. string.format(" · %d ft", math.floor(h * 3.28084 + 0.5))
  end
  local function say(s)
    local g = gidOf(s.u)
    local t = s.t or ""
    if s.x then t = t:gsub("{POS}", function() return where(s.x, s.z) end) end
    if g then trigger.action.outTextForGroup(g, t, s.s or 30) else trigger.action.outTextForCoalition(D.side or 2, t, s.s or 30) end
  end
  local function mark(m)
    local g = gidOf(m.u)
    if not g then return end
    if marks[g] then trigger.action.removeMark(marks[g]) end
    nextMark = nextMark + 1
    marks[g] = nextMark
    trigger.action.markToGroup(nextMark, m.t, { x = m.x, y = land.getHeight({ x = m.x, y = m.z }), z = m.z }, g, true)
  end
  local function pickmenu(m)                                -- Auswahl unter Kampagne: je Eintrag MENU einheit pick schlüssel
    local g = gidOf(m.u)
    if not g or not menus[g] then return end
    if picks[g] then missionCommands.removeItemForGroup(g, picks[g]) end
    picks[g] = missionCommands.addSubMenuForGroup(g, m.t, menus[g])
    for _, it in ipairs(m.items or {}) do missionCommands.addCommandForGroup(g, it.l, picks[g], ask, { m.u, "pick", it.k }) end
  end

  -- Live-Befehle der App über den Hook (DcsCampaignHook.lua): { seq, add = { Einträge wie DCSCAMP.spawn }, del = { Schlüssel } }. Quittung CMD seq adds dels.
  -- Dazu Antworten auf F10: say = { { u, t, s, x, z } }, mark = { { u, t, x, z } }, smoke = { { x, z, c } }, menu = { { u, t, items = { { l, k } } } }.
  function D.cmd(c)
    for _, e in ipairs(c.add or {}) do
      if not byKey[e.k] then S[#S + 1] = e byKey[e.k] = e e.base = e.n end
    end
    for _, k in ipairs(c.del or {}) do
      local e = byKey[k]
      if e and not e.done then
        if e.live then out(e, timer.getTime()) end
        e.done = true
      end
    end
    for _, m in ipairs(c.msg or {}) do trigger.action.outTextForCoalition(D.side or 2, m, 20) end      -- Lage im Funk
    for _, s in ipairs(c.say or {}) do pcall(say, s) end
    for _, m in ipairs(c.mark or {}) do pcall(mark, m) end
    for _, m in ipairs(c.smoke or {}) do pcall(trigger.action.smoke, { x = m.x, y = land.getHeight({ x = m.x, y = m.z }), z = m.z }, m.c or 1) end
    for _, m in ipairs(c.menu or {}) do pcall(pickmenu, m) end
    log("CMD", c.seq, #(c.add or {}), #(c.del or {}))
  end

  local pl = {}                                              -- alle Menschen (Mehrspieler: jeder Client-Platz): x, y, z, g = Gruppen-ID
  local function near(x, z, r)
    if not x then return false end
    for _, q in ipairs(pl) do if (x - q.x) ^ 2 + (z - q.z) ^ 2 < r * r then return true end end
    return false
  end
  local function players()
    local now = {}
    for _, u in ipairs(coalition.getPlayers(D.side or 2) or {}) do
      if u:isExist() then local q = u:getPoint() now[#now + 1] = { x = q.x, y = q.y, z = q.z, g = u:getGroup():getID(), u = u } pcall(kmenu, u) end
    end
    local me = #now == 0 and Unit.getByName(D.player or "")
    if me and me:isExist() then local q = me:getPoint() now[1] = { x = q.x, y = q.y, z = q.z, u = me } pcall(kmenu, me) end
    if #now > 0 then pl = now end                            -- niemand drin (tot, ausgestiegen): letzte Lage behalten
  end
  local function lead(gr)
    for _, u in ipairs(gr:getUnits() or {}) do if u:isExist() then return u:getPoint() end end
  end
  local function dist(x, z)                                  -- Entfernung zum nächsten Menschen
    local m
    for _, q in ipairs(pl) do local d = (x - q.x) ^ 2 + (z - q.z) ^ 2 if not m or d < m then m = d end end
    return m and math.sqrt(m)
  end
  -- Bubble: das Nächste zuerst (gemessen am eigenen Radius), Budget Luft (KI-Maschinen) und Boden (Fahrzeuge, Schiffe) getrennt
  timer.scheduleFunction(function(_, t)
    players()
    if #pl == 0 then return t + 10 end
    local n, cap = { [true] = 0, [false] = 0 }, { [true] = D.maxAir or 40, [false] = D.max or 300 }   -- true = Luft
    for _, e in ipairs(S) do
      if e.live then
        local gr = Group.getByName(e.n)
        local p = gr and gr:isExist() and lead(gr)
        if not p then e.live, e.done = false, true          -- vernichtet oder nach der Landung verschwunden
        elseif not (e.pin and t < e.pin) and not near(p.x, p.z, e.r * 1.25) then out(e, t)
        else n[e.cat < 2] = n[e.cat < 2] + gr:getSize() end
      end
    end
    local want = {}
    for _, e in ipairs(S) do
      if not e.live and not e.done then
        local x, z, _, _, gone = plan(e, t)
        if gone then e.done = true
        elseif x then
          local d = dist(x, z)
          if d and d < e.r then want[#want + 1] = { e = e, f = d / e.r } end
        end
      end
    end
    table.sort(want, function(a, b) return a.f < b.f end)
    for _, c in ipairs(want) do
      local e = c.e
      local air = e.cat < 2
      if not e.live and n[air] < cap[air] then
        n[air] = n[air] + (spawn(e, t) or 0)
        local d = e.live and e.dep and byKey[e.dep]        -- Angriffsziel kommt mit, solange der Angreifer unterwegs ist
        if d and not d.done then
          if not d.live then n[d.cat < 2] = n[d.cat < 2] + (spawn(d, t) or 0) end
          local p = e.g.route.points
          d.pin = math.max(d.pin or 0, p[#p].ETA or 0)
        end
      end
    end
    return t + 10
  end, nil, timer.getTime() + 4)

  -- Spuren des Krieges: Rauch (brennende Wracks, Depots, Kampf an der Front), Pistenkrater auf beschädigten Plätzen
  timer.scheduleFunction(function()
    for _, s in ipairs(D.smoke or {}) do
      pcall(trigger.action.effectSmokeBig, { x = s.x, y = land.getHeight({ x = s.x, y = s.z }), z = s.z }, s.p, 0.8)
    end
    for _, c in ipairs(D.craters or {}) do
      trigger.action.explosion({ x = c.x, y = land.getHeight({ x = c.x, y = c.z }), z = c.z }, 300)
    end
  end, nil, timer.getTime() + 3)

  -- Artillerie in der Bubble schießt alle 5 min auf die feindliche Front (danach weiter wie geplant)
  timer.scheduleFunction(function(_, t)
    for _, e in ipairs(S) do
      local gr = e.live and e.fire and Group.getByName(e.n)
      if gr and gr:isExist() then
        pcall(function() gr:getController():pushTask({ id = "FireAtPoint", params = { point = { x = e.fire.x, y = e.fire.z }, radius = 300, expendQty = 6, expendQtyEnabled = true } }) end)
      end
    end
    return t + 300
  end, nil, timer.getTime() + 60)

  -- IADS (Skynet-Prinzip): Batterien (iads = Reichweite) bleiben dunkel, bis ein Ziel in Reichweite und in Sicht ist und ihr Netz lebt
  -- (net = Frühwarnradare/AWACS, die sie laut Kampagne abdecken). Ohne Netz autonom: Radar an wie in DCS üblich.
  -- Radar-Flugkörper im Anflug: Batterien bis 30 km schalten mit 60 % für 1–2 min ab (kein Ziel für den HARM, schießen aber auch nicht).
  local function emit(gr, on)
    pcall(gr.enableEmission, gr, on)
    pcall(function() gr:getController():setOption(AI.Option.Ground.id.ALARM_STATE, on and AI.Option.Ground.val.ALARM_STATE.RED or AI.Option.Ground.val.ALARM_STATE.GREEN) end)
  end
  -- Störer (Kampagnenflüge "Jammer", D.jam) bis 120 km: Batterie sieht nur innerhalb der Burn-through-Entfernung (40 % ihrer Reichweite)
  timer.scheduleFunction(function(_, t)
    local hp, air, jams = {}, {}, {}
    for _, j in ipairs(D.jam or {}) do
      local g = Group.getByName(j.n)
      local p = g and g:isExist() and lead(g)
      if p then jams[#jams + 1] = { s = j.s, p = p } end
    end
    for i = #harms, 1, -1 do
      local ok, p = pcall(function() return harms[i]:isExist() and harms[i]:getPoint() end)
      if ok and p then hp[#hp + 1] = { w = harms[i], p = p } else table.remove(harms, i) end
    end
    local function foes(s)                                     -- Flugzeuge/Hubschrauber der Gegenseite in der Luft, je Takt einmal
      if not air[s] then
        air[s] = {}
        for _, cat in ipairs({ 0, 1 }) do
          for _, g in ipairs(coalition.getGroups(3 - s, cat) or {}) do
            for _, u in ipairs(g:getUnits() or {}) do
              local ok, p = pcall(function() return u:isExist() and u:inAir() and u:getPoint() end)
              if ok and p then air[s][#air[s] + 1] = p end
            end
          end
        end
      end
      return air[s]
    end
    for _, e in ipairs(S) do
      local gr = e.live and (e.iads or 0) > 0 and Group.getByName(e.n)
      local c = gr and gr:isExist() and lead(gr)
      if c then
        e.seen = e.seen or {}
        for _, h in ipairs(hp) do
          if not e.seen[h.w] and (h.p.x - c.x) ^ 2 + (h.p.z - c.z) ^ 2 < 9e8 then
            e.seen[h.w] = true
            if math.random() < 0.6 then e.dark = t + 60 + math.random(60) log("SAM", e.k, "harm") end
          end
        end
        local net = false
        for _, k in ipairs(e.net or {}) do if not lost[k] then net = true end end
        if not net and #(e.net or {}) > 0 and not e.auto then e.auto = true log("SAM", e.k, "auto") end
        local side, jam = coalition.getCountryCoalition(e.c), false
        for _, j in ipairs(jams) do if j.s ~= side and (j.p.x - c.x) ^ 2 + (j.p.z - c.z) ^ 2 < 1.44e10 then jam = true break end end
        if jam ~= (e.jam or false) then e.jam = jam log("SAM", e.k, jam and "jam" or "clear") end
        local on = not net and not jam
        if (e.dark or 0) > t then on = false
        elseif net or jam then
          local eye, r = { x = c.x, y = c.y + 20, z = c.z }, e.iads * (jam and 0.4 or 1.15)
          for _, p in ipairs(foes(side)) do
            if (p.x - c.x) ^ 2 + (p.z - c.z) ^ 2 < r ^ 2 and land.isVisible(eye, p) then on = true break end
          end
        end
        if e.on ~= on then e.on = on emit(gr, on) end
      end
    end
    return t + 5
  end, nil, timer.getTime() + 5)

  -- AWACS-Lagebild alle 5 min: feindliche Luftgruppen bis 120 NM, nächste als Peilung/Entfernung/Höhe vom Spieler
  if D.awacs then
    local foe = D.side == 1 and coalition.side.BLUE or coalition.side.RED
    timer.scheduleFunction(function(_, t)
      for _, q in ipairs(pl) do                                -- Bild je Spieler (Peilung von seiner Position)
        local n, best, bd = 0, nil, 222240
        for _, g in ipairs(coalition.getGroups(foe, Group.Category.AIRPLANE) or {}) do
          local p = lead(g)
          local d = p and math.sqrt((p.x - q.x) ^ 2 + (p.z - q.z) ^ 2)
          if d and d < 222240 then
            n = n + 1
            if d <= bd then bd, best = d, p end
          end
        end
        if n > 0 then
          local brg = math.floor((math.deg(math.atan2(best.z - q.z, best.x - q.x)) + 360) % 360 + 0.5) % 360
          local msg = string.format("%s: picture, %d group%s hostile. Nearest BRA %03d / %d NM / %d000 ft.",
            D.awacs, n, n > 1 and "s" or "", brg, math.floor(bd / 1852 + 0.5), math.floor(best.y / 304.8 + 0.5))
          if #pl > 1 and q.g then trigger.action.outTextForGroup(q.g, msg, 15) else trigger.action.outTextForCoalition(D.side, msg, 15) end
        end
      end
      return t + 300
    end, nil, timer.getTime() + 90)
  end

  -- Takt für die Kriegsuhr + Positionen: Kampagnenflüge (Gruppen "F<id> ...") und Fahrplan-Bodengruppen in der Bubble
  timer.scheduleFunction(function(_, t)
    log("T")
    for _, side in ipairs({ coalition.side.RED, coalition.side.BLUE }) do
      for _, g in ipairs(coalition.getGroups(side) or {}) do        -- Flugzeuge und Hubschrauber (Name "F<id> ...")
        local gn = name(g)
        if gn and gn:match("^F%d+ ") then
          for _, u in ipairs(g:getUnits() or {}) do
            if u:isExist() and u:getLife() > 0 then
              local p = u:getPoint()
              log("POS", gn, f0(p.x), f0(p.z), f0(p.y))
              break
            end
          end
        end
      end
    end
    for _, e in ipairs(S) do
      local gr = e.live and e.cat > 1 and Group.getByName(e.n)
      local p = gr and gr:isExist() and lead(gr)
      if p then log("POS", e.n, f0(p.x), f0(p.z), "0") end
    end
    return t + 60
  end, nil, timer.getTime() + 5)

  -- ---------- Mehrspieler: verlassene Maschinen fliegt die KI heim, Spieler können sie wieder übernehmen ----------
  -- Mensch verlässt F12-2 in der Luft → KI-Ersatz "F12-2K" an derselben Stelle fliegt zum Heimatplatz (DCSCAMP.homes) und landet;
  -- alle 30 s liegt an seiner Position ein freier Spielerplatz "F12-2U". Steigt dort jemand ein, verschwindet der KI-Ersatz.
  -- ponytail: Ersatz fliegt ohne Waffen (Stationen bräuchten CLSIDs); ob DCS Plätze zur Laufzeit anbietet, zeigt der Test unten.
  local heim = {}                     -- "F12-2" -> { t = Typ, c = Land }
  local function plane(nm, c, t, skill, q, v, fuel, h)
    local hdg, spd = math.atan2(v.z, v.x), math.max(120, math.sqrt(v.x ^ 2 + v.z ^ 2))
    local pts = { { x = q.x, y = q.z, alt = q.y, alt_type = "BARO", type = "Turning Point", action = "Turning Point", speed = spd } }
    if h then pts[2] = { x = h.x, y = h.z, alt = 0, alt_type = "BARO", type = "Land", action = "Landing", airdromeId = h.ad, speed = 140 } end
    coalition.addGroup(c, Group.Category.AIRPLANE, { name = nm, task = "CAP", route = { points = pts },
      units = { { name = nm, type = t, skill = skill, x = q.x, y = q.z, alt = q.y, alt_type = "BARO", speed = spd, heading = hdg, psi = -hdg,
                  payload = { pylons = {}, fuel = fuel, flare = 0, chaff = 0, gun = 0 } } } })
  end
  mp[E.S_EVENT_PLAYER_LEAVE_UNIT] = function(o)
    local n = name(o)
    local h = n and not dead[n] and D.homes and D.homes[n:match("^(F%d+)%-%d+$") or ""]
    local okA, air = pcall(o.inAir, o)
    if not (h and okA and air) then return end                      -- am Boden, ausgestiegen, tot: nichts zu ersetzen
    gone[n] = timer.getTime()
    block(n)
    local ok, err = pcall(function()
      local t, q, v = o:getTypeName(), o:getPoint(), o:getVelocity()
      local fuel = (o:getFuel() or 0.5) * ((o:getDesc() or {}).fuelMassMax or 3000)
      plane(n .. "K", h.c, t, "High", q, v, fuel, h)
      heim[n] = { t = t, c = h.c }
      D.blocked[n .. "U"] = nil
    end)
    log("AIHOME", n, ok and "ok" or tostring(err))
    if not ok then gone[n] = nil dead[n] = true log("DEAD", n, pos(o)) end         -- kein Ersatz: Maschine verloren
  end
  mp[E.S_EVENT_BIRTH] = function(o)
    local r, n = raw(o), name(o)
    if not r or r:sub(-1) ~= "U" then return end
    seated[r] = true
    heim[n] = nil
    local k = Group.getByName(n .. "K")
    if k then gone[n] = timer.getTime() k:destroy() end
  end
  timer.scheduleFunction(function(_, t)
    for n, r in pairs(heim) do
      local k = Unit.getByName(n .. "K")
      if not (k and k:isExist()) or D.blocked[n .. "U"] then heim[n] = nil    -- KI gelandet oder abgeschossen
      else
        local q, v = k:getPoint(), k:getVelocity()
        seated[n .. "U"] = nil
        local ok, err = pcall(plane, n .. "U", r.c, r.t, "Client", { x = q.x, y = q.y + 150, z = q.z }, v, (k:getFuel() or 0.5) * ((k:getDesc() or {}).fuelMassMax or 3000))
        if not ok then env.error("DCSCAMP slot " .. n .. ": " .. tostring(err)) end
      end
    end
    return t + 30
  end, nil, timer.getTime() + 30)

  -- Mehrspieler-Test (Plan Phase 0, Punkt 8): kann ein Skript einen Spielerplatz an beliebiger Stelle anlegen?
  if D.mp then
    missionCommands.addCommandForCoalition(D.side, "Test: create player slot here", nil, function()
      local ok, err = pcall(function()
        local u = pl[1] and pl[1].u
        if not (u and u:isExist()) then error("no player airborne") end
        local q, v = u:getPoint(), u:getVelocity()
        local h = math.atan2(v.z, v.x)
        local x, z = q.x + 2000 * math.cos(h), q.z + 2000 * math.sin(h)
        coalition.addGroup(u:getCountry(), Group.Category.AIRPLANE, { name = "TEST Slot", task = "CAP",
          units = { { name = "TEST Slot-1", type = u:getTypeName(), skill = "Client", x = x, y = z, alt = q.y, alt_type = "BARO", speed = 200, heading = h,
                      payload = { pylons = {}, fuel = 4000, flare = 30, chaff = 30, gun = 100 } } },
          route = { points = { { x = x, y = z, alt = q.y, alt_type = "BARO", type = "Turning Point", action = "Turning Point", speed = 200 } } } })
      end)
      log("TESTSLOT", ok and "created" or tostring(err))
      trigger.action.outTextForCoalition(D.side, ok and "Test slot 'TEST Slot' created - open slot selection to check." or ("Test slot failed: " .. tostring(err)), 20)
    end)
  end

  log("START")
end
