-- Offline-Test mod\DcsCampaignHook.lua: Befehlsdatei wird gelesen, gelöscht und als a_do_script in die Mission gereicht.
-- "C:\Program Files\Eagle Dynamics\DCS World\bin\luae.exe" tests\hooktest.lua mod\DcsCampaignHook.lua
local files = { ["00002.lua"] = 'x = 2', ["00001.lua"] = 'DCSCAMP_CMD = { ["s"] = "a\\"b\nc" }\nif DCSCAMP then end', ["00003.lua.tmp"] = 'halb' }
local clock, sent, removed, cb = 0, {}, {}, nil
os.clock = function() return clock end
os.getenv = function(k) return k == "TMP" and "C:\\Tmp" or nil end
os.remove = function(p) removed[#removed + 1] = p files[p:match("[^\\]+$")] = nil end
local written = {}
io.open = function(p, m)
  if m == 'w' then return { write = function(_, ...) written[p] = (written[p] or '') .. table.concat({...}) end, close = function() end } end
  local t = files[p:match("[^\\]+$")] return t and { read = function() return t end, close = function() end } end
package.preload.me_db_api = function() return { db = { Weapons = { Categories = {
  { Launchers = { { CLSID = '{AIM-9M}', wsTypeOfWeapon = { 4, 4, 7, 6 }, Count = 1 }, { CLSID = '{BRU55}', wsTypeOfWeapon = { 4, 5, 36, 86 }, Count = 2 } } },
  { Launchers = { { CLSID = '{Tank}', attribute = { 1, 3, 43, 10 } }, { CLSID = '{Kaputt}', wsTypeOfWeapon = { 4, 4, 'x', 1 } } } } } } } } end
package.preload.lfs = function() return {
  attributes = function(p) return p == "C:\\Tmp\\DcsCampaign-Cmd" end,
  dir = function() local k = {} for n in pairs(files) do k[#k + 1] = n end local i = 0 return function() i = i + 1 return k[i] end end } end
net = { log = function() end, dostring_in = function(env, s) sent[#sent + 1] = { env, s } end }
DCS = { setUserCallbacks = function(c) cb = c end }
dofile(arg[1])

local fail = 0
local function ok(c, m) if not c then print("FEHLER: " .. m) fail = fail + 1 end end
cb.onSimulationFrame()
ok(#sent == 2 and sent[1][1] == "mission", "zwei Befehle an die Mission")
ok(#removed == 2 and files["00003.lua.tmp"], "gelesene Dateien gelöscht, halb geschriebene liegen gelassen")
local wl = written["C:\\Tmp\\DcsCampaign-Weapons.txt"]
ok(wl == "{AIM-9M};4;4;7;6;1\n{BRU55};4;5;36;86;2\n{Tank};1;3;43;10;1\n", "Waffenliste: CLSID -> wsType der Waffe, Anzahl; ohne Zahlen-wsType ausgelassen: " .. tostring(wl))
files["00004.lua"] = 'y = 4'
clock = 0.5 cb.onSimulationFrame()
ok(#sent == 2, "höchstens einmal je Sekunde abfragen")
clock = 1.2 cb.onSimulationFrame()
ok(#sent == 3, "nächster Befehl nach 1 s")
local got = {}
_G.a_do_script = function(code) got[#got + 1] = code end
for _, s in ipairs(sent) do assert(loadstring(s[2]))() end
ok(got[1] == 'DCSCAMP_CMD = { ["s"] = "a\\"b\nc" }\nif DCSCAMP then end' and got[2] == 'x = 2' and got[3] == 'y = 4', "Befehle unverändert und in Reihenfolge")
-- Mehrspieler: Mission laden, gesperrte Plätze
local loaded, chat
files["load.miz"] = 'C:\\M\\Kampagne 1.miz'
net.load_mission = function(p) loaded = p end
clock = 2.5 cb.onSimulationFrame()
ok(loaded == 'C:\\M\\Kampagne 1.miz' and not files["load.miz"], "Mehrspieler: Server lädt die neue Mission")
DCS.UNIT_NAME = 3
DCS.getUnitProperty = function(id, prop) return prop == 3 and (id == "7" and "F12-2" or "F12-3") or nil end
net.dostring_in = function(env, s) return env == "server" and s:find('"F12-2"', 1, true) and "1" or "" end
net.send_chat_to = function(m, id) chat = id end
ok(cb.onPlayerTryChangeSlot(5, 2, "7") == false and chat == 5, "gesperrter Platz abgelehnt, mit Hinweis")
ok(cb.onPlayerTryChangeSlot(6, 2, "8") == nil, "freier Platz erlaubt")
-- Mehrspieler-Client auf demselben PC: Befehle liegen lassen (der Server holt sie)
files["00005.lua"] = 'z = 5'
DCS.isMultiplayer = function() return true end
DCS.isServer = function() return false end
local n0 = #removed
clock = 4 cb.onSimulationFrame()
ok(#removed == n0 and files["00005.lua"], "Client lässt Befehle für den Server liegen")
DCS.isServer = function() return true end
clock = 5 cb.onSimulationFrame()
ok(#removed == n0 + 1, "Server führt sie aus")
print(fail == 0 and "ALLES OK" or (fail .. " FEHLER"))
os.exit(fail == 0 and 0 or 1)
