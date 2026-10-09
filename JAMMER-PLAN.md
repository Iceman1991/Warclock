# Jammer-Plan: realistisches Stören gegen SAMs (KI-Störer und Spieler)

Stand 2026-10-09 · nur Plan, noch nichts umgesetzt.

## Befund: warum es sich in DCS nach „wirkt nicht“ anfühlt

- **Vanilla-ECM ist ein fester Schwellwert.** Außerhalb der Burn-through-Entfernung (je Muster etwa 10 NM) fehlt dem Radar nur die Entfernung (Range Denial). Es gibt keine J/S-Rechnung und keine Täuschung von Winkeln. Ein Teil der Werte steckt im nativen Code, einiges ist undokumentiert.
- **Die Wirkung gegen KI-SAMs schwankt je Typ.** In der Community gilt sie als kaum spürbar.
- **Ein Skript kann den ECM-Schalter des Spielers nicht lesen.** Die Felder `Flags.Jamming` und `ECM_on` gibt es nur in der Export-Umgebung.
- **Es gibt keinen echten Störer im Spiel.** EA-6B und EA-18G fehlen sowohl als KI als auch flyable. Vorhanden sind nur Selbstschutz-Pods:
  - ALQ-131, ALQ-184, ALQ-184 Long, SPS-141, L005 Sorbtsiya, Sky-Shadow
  - interne Systeme: FA-18C (ALQ-165), F-15E, M-2000C, JF-17, F-16C
- **Was die Kampagne heute tut:**
  - Ein „Jammer“-Flug im Umkreis von 120 km bringt jede Batterie auf 40 % Reichweite, pauschal für alle Ziele (`mod/DcsCampaign.lua` ~400).
  - In der abstrakten Simulation gilt dasselbe, dazu Pk ×0,6 (`Air.cs:1335`).
  - Spieler-ECM zählt gar nicht.

**Fazit:** Wir rechnen das Stören selbst und setzen es mit Missionsskript-Hebeln durch. Vanilla-ECM läuft parallel weiter. Unser Modell kann dadurch nur einschränken, nie zusätzlich freigeben.

## Modell (eine Quelle in C#, als Tabelle in die Mission geschrieben)

### Physik in einfacher Form (Radargleichung)

Zwei Fälle:

- **Selbstschutz (SPJ, Pod am Ziel selbst):**
  - Formel: `J/S ∝ R²` ⇒ `R_bt = R_max · f_radar · √(σ_aspekt / J_pod)`
  - Je stärker der Störer, desto näher kommt das Ziel heran, bevor das Radar durchbrennt. Je größer die Radarsignatur, desto früher brennt es durch.
- **Abstandsstörer (SOJ, Störer weit weg):**
  - Formel: `R_bt = R_max · f_radar · √(R_jammer / R_ref) · J^(-¼) · Keule(θ)`
  - Je weiter der Störer vom Radar entfernt ist, desto schwächer wirkt er.

Faktoren:

- **Keule(θ):**
  - Hauptkeule bei θ < 10° zwischen Radar→Störer und Radar→Ziel: Faktor 1.
  - Nebenkeule: Faktor ~2,5. Gegen Feuerleitradare wirkt das deutlich schwächer.
  - Rundsuchradare fegen mit der Hauptkeule ohnehin über den Störer. Sie werden immer gestört, als Strobe.
- **Mehrere Störer:** Ihre Leistungen J addieren sich.
- **Signatur σ nach Aspekt:** Nase 1, seitlich 3, Heck 1,5. Wer von vorn anfliegt, ist länger geschützt.
- **Rauschen:** ±15 % je Takt, für schwankende Signatur und Geometrie.
- **Frequenzband:** Fighter-Pods decken etwa 2–18 GHz (E–J) ab. Diese Radare sind von Pods praktisch nicht gestört:
  - VHF/UHF-Frühwarnradare (P-18, 55G6, 1L13)
  - L-Band (FPS-117)
  - Sie werden nur von SOJ-Klassen mit Low-Band gestört.

### Radartabelle

Alle Werte sind Stellknöpfe zum Tunen. `ECM_K` aus den DCS-Lua-Dateien ist nur Referenz, seine Bedeutung ist unklar.

| System | Band | f_radar (SPJ) | Lenkung | HOJ |
|---|---|---|---|---|
| SA-2 SNR-75 | G | 0,25 | Kommando | – |
| SA-3 SNR-125 | I | 0,30 | Kommando | – |
| SA-5 5N62 | H | 0,35 | SARH | ✓ |
| SA-6 1S91 | G/H | 0,35 | SARH | – |
| SA-8 Osa | H/J | 0,45 | Kommando | – |
| SA-11 9S35 | H/I | 0,40 | SARH | (prüfen) |
| SA-10 30N6 | I | 0,55 | TVM | ✓ |
| SA-15 Tor / Pantsir | H/J | 0,60 | Kommando | – |
| Hawk HPIR | J | 0,40 | SARH | ✓ |
| Patriot MPQ-53 | G | 0,60 | TVM | ✓ |
| NASAMS | X | 0,55 | ARH | – |
| EWR (P-19, 55G6, 1L13, FPS-117) | VHF/L | Pod: 1,0 · SOJ: 0,5 | – | – |

### Störer-Klassen

| Klasse | Leistung J | Art | Bänder |
|---|---|---|---|
| Fighter-Pod (ALQ-131/184, SPS-141, Sky-Shadow, KG600) | 1 | SPJ | E–J |
| Intern (ALQ-165, ALQ-135, Spectra) | 1,2 | SPJ | E–J |
| L005 Sorbtsiya | 1,2 | SPJ | E–J |
| Growler/Prowler-Ersatz (ALQ-99-Klasse) | 8 | SOJ/EJ | VHF–J |
| Su-24MP, Il-22PP | 6 | SOJ | VHF–J |

Kein Modell vorhanden? Dann fliegen Kampagnen-„Jammer“ mit FA-18C/F-16C als Ersatz und bekommen die SOJ-Klasse zugewiesen. Im Briefing steht ehrlich „EA-18G (simulated)“.

### Home-on-Jam

Bei Systemen mit HOJ (S-200, S-300, Hawk, Patriot) gilt: Ein Störer innerhalb von 60 % der Reichweite wird mit Winkel bekämpft, ohne Entfernung. Die Sperre für ihn fällt dann, mit Pk ×0,8. Folge: Störer müssen Abstand halten, und Selbstschutz-Störer mit Pod sind gegen diese Systeme nur bedingt sicher.

## Durchsetzung im Missionsskript

Erweitert den vorhandenen 5-s-IADS-Takt.

1. **Effektive Reichweite je Ziel.** Jede Batterie rechnet für jedes Luftziel `R_eff = R_max` (ungeschützt) oder `R_bt` (aus SPJ/SOJ wie oben). Das ersetzt die pauschalen 40 % und die 120-km-Regel. Die Radar-Ein-Logik nutzt `R_eff` statt `e.iads·0,4`.
2. **Grobfilter (spart vergeudete Flugkörper).**
   - Liegt kein Ziel innerhalb seines `R_eff`: ROE `WEAPON_HOLD`. Das Radar bleibt an; ein gestörtes Radar strahlt weiter und bleibt HARM-Ziel.
   - Zusätzlich Option 24 `AC_ENGAGEMENT_RANGE_RESTRICTION` = max(`R_eff`)/`R_max` in Prozent.
   - Option 24 ist nur aus dem Hoggit-Wiki belegt und muss im Spiel geprüft werden. Fallback: nur ROE.
3. **Schuss-Prüfung (genau je Ziel).**
   - `S_EVENT_SHOT` mit SAM-Flugkörper: `weapon:getTarget()` ist geschützt und außerhalb seines `R_eff` ⇒ „kein Lock“.
   - Der Flugkörper fliegt 2–6 s und zerlegt sich dann: kleine `trigger.action.explosion` an seiner Position plus `destroy`.
   - Er verschwindet also nicht schlagartig, sondern wirkt wie ein Selbstzerleger nach Lock-Verlust.
4. **Lock-Verlust im Flug.**
   - Lenkflugkörper Richtung geschütztes Ziel: je Takt Prüfung, ob das Ziel jetzt außerhalb von `R_bt` liegt, etwa weil ECM gerade eingeschaltet wurde oder der Störer näher kam.
   - Wahrscheinlichkeit für Lock-Verlust je Takt: Kommando 35 %, SARH 25 %, TVM 15 %, ARH 10 % (ARH im Endanflug nicht mehr).
5. **Strobe statt Track im IADS.**
   - Ein gestörtes Frühwarnradar liefert dem Netz nur die Peilung. Das Netz geht auf Alarm, die Batterien schalten ihre Radare auf.
   - Schießen ist aber erst innerhalb von `R_eff` möglich. Stören verrät also die Anwesenheit, aber nicht die Entfernung.
6. **Debrief-Zeilen über die Bridge**, z. B.:
   - `"SA-11 Buk (Gori): jammed – lost lock on Ford 2-1"`
   - `"… burn-through at 14 km"`
   - `"… home-on-jam shot at Pontiac 2-1"`

## Spieler-ECM (muss wirklich funktionieren)

1. **Fähigkeit aus der Beladung.** Der Missionsgenerator kennt die Pylonen des Spielers: Pod-CLSIDs aus `pods.lua` oder internes System je Muster. Daraus entsteht `D.ecm[unit] = Klasse`. Ohne Störsystem gibt es kein Stören, auch keinen F10-Eintrag.
2. **Schalterzustand, automatisch für den Spieler am DCS-Rechner (Singleplayer und Host).**
   - Der optionale Hook (GameGUI) liest jede Sekunde `Export.LoGetSelfData()` bzw. die Flags `Jamming`/`ECM_on` des eigenen Flugzeugs.
   - Er schickt den Zustand per `net.dostring_in('mission', …)` in die Mission.
   - Ergebnis: Wer im Cockpit ECM einschaltet, stört auch in unserem Modell.
   - Muss je Muster im Spiel geprüft werden: Hornet ASPJ, Viper-Pod, F-15E, FC3.
3. **Fallback für MP-Clients und für den Fall, dass der Export nichts liefert:**
   - F10 → Campaign → „ECM: on/off“ je Spieler, Standard aus.
   - Schalten nur in der Luft. Kurze Meldung „ECM transmitting / standby“.
   - Später optional über den DCS-ATC-Client: Schnittstellen-Idee in `ATC-SCHNITTSTELLE.md`, DCS-ATC-Dateien bleiben unberührt.
4. **Wirkung wie oben (SPJ).**
   - Feuerleitradare sehen den Spieler erst innerhalb von `R_bt`.
   - Raketen im Flug können den Lock verlieren.
   - HOJ-Systeme schießen auf den Störer.
   - ECM an weckt das IADS (Strobe). Wer unentdeckt bleiben will, lässt es aus und fliegt tief.
5. **Planungshilfe (BMS-artig).**
   - Im Flight-Fenster: „ECM: ALQ-184 pod (self-protection)“.
   - Auf der Karte für den gewählten Flug: Burn-through-Ring als gestrichelter Innenring in jedem bekannten SAM-Ring.
   - Schalter „ECM on“ in der Kartenebene, um beide Ringe zu vergleichen.
6. **Debrief:** „Your ECM denied 3 SAM shots, 1 missile lost lock, burned through by SA-10 at 31 km“.

## Abstrakte Simulation (Air.cs) angleichen

- Gleiche Funktion `Ew.Bt(radar, ziel, störer[])` aus C#. `Air.cs:1335` ersetzt `d > Range·0,4` durch `d > R_eff`.
- Pk bei Treffer innerhalb von `R_eff`, aber im gestörten Fall: ×0,8 (SPJ) bzw. ×0,7 (SOJ-Hauptkeule).
- HOJ gegen den Störer selbst.
- KI-Flüge mit Pod in der Beladung (Escort, SEAD, Strike) zählen als SPJ. Damit wirkt Stören auch in Stunden, in denen niemand fliegt.
- Planer der KI: Jammer-Flüge nur gegen Radare, bei denen es lohnt (Band, f_radar). Die SOJ-Bahn liegt auf der Achse Bedrohung→Paket, also in der Hauptkeule, in 60–80 % der HOJ-Grenze.

## Phasen

1. **Modell + Tests (offline):**
   - `Ew.cs` mit Tabellen und Formel; Air.cs angleichen.
   - `--test`-Checks:
     - SPJ verkürzt die SA-2-Reichweite deutlich.
     - SOJ wird mit dem Abstand schwächer.
     - Hauptkeule wirkt stärker als Nebenkeule.
     - Pod gegen VHF-EWR bleibt ohne Wirkung.
     - HOJ greift innerhalb von 60 %.
2. **Missionsskript:** `D.ew`-Tabelle, `R_eff` je Ziel, ROE/Option 24, Schuss-Prüfung, Lock-Verlust, Strobe, Debrief-Zeilen. MizTest prüft die Tabellen.
3. **Spieler:** Beladung → Fähigkeit, Hook-Auslesen, F10-Fallback, Flight-Fenster, Karten-Ring, Debrief.
4. **KI-Störer:** Leistungsklassen, SOJ-Geometrie, Escort Jamming, Briefing-Kennzeichnung.
5. **Prüfung im Spiel (du):** Szenarien
   - SA-11 mit und ohne Pod
   - SA-10 gegen den Growler-Ersatz in 80 km Abstand
   - Hawk-HOJ
   - Option 24 wirksam?
   - Exportfelder je Muster

   Danach die Stellknöpfe tunen.

## Risiken / offen

- Option 24 ist lokal nicht belegt. Wenn sie nicht greift: nur ROE und Schuss-Prüfung (funktioniert immer, kostet aber Flugkörper).
- Zerlegen nach dem Start kann „gamey“ wirken. Deshalb verzögert, mit Explosion und Debrief-Begründung.
- MP-Clients ohne Export: F10 bleibt die Wahrheit. Wer im Cockpit ECM an hat, aber F10 aus, bekommt nur Vanilla-Wirkung.
- SA-11-HOJ und die genauen Bänder sind Annahmen und in der Tabelle als Stellknöpfe markiert.
