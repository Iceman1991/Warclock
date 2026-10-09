-- DCS-Kampagne (Hook, optional): reicht Live-Befehle der App in die laufende Kampagnenmission – neue Flüge, Verbände, Konvois, Entfernen.
-- Ort: Saved Games\DCS\Scripts\Hooks (wie DCS-ATC und SRS), keine Änderung an DCS-Dateien. Die App legt Lua-Befehle als
-- %TMP%\DcsCampaign-Cmd\*.lua ab; der Hook führt sie per net.dostring_in('mission', 'a_do_script(...)') im Missionsskript aus und löscht sie.
-- Ohne Kampagnenmission passiert nichts (jeder Befehl prüft das Token der Mission).
-- Mehrspieler: load.miz enthält den Pfad der neuen Kampagnenmission – der Server lädt sie (net.load_mission).
-- Platzwahl: Maschinen, die gelandet, verloren oder von der KI heimgeflogen werden (DCSCAMP.blocked), sind gesperrt.
-- Nur der Server (oder Einzelspieler) führt Befehle aus; ein beigetretener Client auf demselben PC lässt sie liegen.
-- Waffen-Lager: einmal je DCS-Start CLSID -> wsType der Waffe aus der DCS-Datenbank nach %TMP%\DcsCampaign-Weapons.txt (Platzlager in der Mission).
local lfs = require('lfs')
local dir = (os.getenv('TMP') or os.getenv('TEMP') or '') .. '\\DcsCampaign-Cmd'
local cb, nextPoll, exported = {}, 0, false
local Sim = Sim or DCS

local function exportWeapons()
  exported = true
  local ok, DB = pcall(require, 'me_db_api')
  if not ok or type(DB) ~= 'table' or type(DB.db) ~= 'table' or not DB.db.Weapons then return end
  local out = {}
  for _, cat in pairs(DB.db.Weapons.Categories or {}) do
    for _, l in pairs(cat.Launchers or {}) do
      local w = l.wsTypeOfWeapon or l.attribute
      if type(l.CLSID) == 'string' and type(w) == 'table' and type(w[1]) == 'number' and type(w[2]) == 'number' and type(w[3]) == 'number' and type(w[4]) == 'number' then
        out[#out + 1] = string.format('%s;%d;%d;%d;%d;%d', l.CLSID, w[1], w[2], w[3], w[4], tonumber(l.Count) or 1)
      end
    end
  end
  if #out == 0 then return end
  local h = io.open((os.getenv('TMP') or os.getenv('TEMP') or '') .. '\\DcsCampaign-Weapons.txt', 'w')
  if h then h:write(table.concat(out, '\n'), '\n') h:close() end
  net.log('DcsCampaignHook: ' .. #out .. ' launchers exported')
end

local function client() return DCS.isMultiplayer and DCS.isMultiplayer() and DCS.isServer and not DCS.isServer() end

function cb.onSimulationFrame()
  if not exported then pcall(exportWeapons) end
  local now = os.clock()
  if now < nextPoll then return end
  nextPoll = now + 1
  if client() or not lfs.attributes(dir) then return end
  local files = {}
  for f in lfs.dir(dir) do if f:sub(-4) == '.lua' or f == 'load.miz' then files[#files + 1] = f end end
  table.sort(files)
  for _, f in ipairs(files) do
    local path = dir .. '\\' .. f
    local h = io.open(path, 'r')
    if h then
      local code = h:read('*a')
      h:close()
      os.remove(path)
      local ok, err
      if f == 'load.miz' then ok, err = pcall(net.load_mission, code)
      else ok, err = pcall(net.dostring_in, 'mission', 'a_do_script(' .. string.format('%q', code) .. ')') end
      if not ok then net.log('DcsCampaignHook: ' .. tostring(err)) end
    end
  end
end

function cb.onPlayerTryChangeSlot(playerID, side, slotID)
  local ok, name = pcall(DCS.getUnitProperty, slotID, DCS.UNIT_NAME)
  if not ok or type(name) ~= 'string' then return end
  local ok2, r = pcall(net.dostring_in, 'server', 'return DCSCAMP and DCSCAMP.blocked and DCSCAMP.blocked[' .. string.format('%q', name) .. '] and "1" or ""')
  if ok2 and r == '1' then
    net.send_chat_to('Campaign: this aircraft has landed, is lost or is flying home under AI control - please choose another slot.', playerID)
    return false
  end
end

Sim.setUserCallbacks(cb)
net.log('DcsCampaignHook loaded')
