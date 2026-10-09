# Schnittstelle DCS-Kampagne → DCS-ATC (Funksprüche)

Die Kampagne beantwortet das F10-Menü „Kampagne“ (Lage, JTAC, Pop-up-Ziel, nächster Einsatz) als Text im Spiel.
Sprechen soll das DCS-ATC. Dafür hängt die Kampagne jeden Funkspruch als Zeile an:

`%TEMP%\DcsCampaign-Funk.txt` (UTF-8, eine Zeile je Spruch, nur anhängen; die DCS-ATC liest ab ihrer letzten Position)

```
Zeit;Einheit;Rolle;Absender;Text
2026-10-12T08:45:00;F57-1;jtac;Axeman;Colt 9, Axeman: CAS-Auftrag. / Ziel: 1. Panzer-Btl. … / Lage: x=-281234 z=647890 / …
```

| Feld | Inhalt |
|---|---|
| Zeit | Kriegszeit (Missionszeit), ISO 8601 ohne Zone |
| Einheit | DCS-Einheit des Spielers, der gefragt hat (`F12-1`), Gruppe = alles vor dem Bindestrich |
| Rolle | `jtac` (Bodenleitstelle, Frequenz der CAS), `awacs` (Umleitung im Flug), `lage` (Kommandostand/Platz) |
| Absender | Rufname: JTAC-Name, AWACS-Rufname oder „Kampagne“/Platzname |
| Text | Zeilen mit ` / ` getrennt; Koordinaten als `x=… z=…` (DCS-Weltkoordinaten, für LL/MGRS umrechnen) |

Die DCS-ATC entscheidet selbst, ob und wie sie spricht (Stimme je Rolle, Brevity, Kürzen). Die Kampagne zeigt den Text
immer zusätzlich im Spiel an; ohne DCS-ATC bleibt es beim Text.
