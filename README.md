# Footplate Inspection Reports

Windows and Android app (.NET MAUI) for **night / day footplate inspection notes** of the Kota – COR section, West Central Railway.

**Author:** DHARAM PAL JANGID, Senior Section Engineer (P.Way) Kota Division of WCRly

## What it does

- **Report form** in the order of the inspection note: particulars (date, kind and purpose, train, from – to, departure / arrival, footplate time and km), loco and crew on board (with HQ), field staff seen en-route (gate keepers, watchmen, patrolmen, keymen – location, nos., alert / sleeping / without jacket), standard observations (tick), visibility and weather, shortcomings, locations to be attended for better running, remarks.
- **Official layout** – Railway / Division / Office heading, two-column particulars, crew table, numbered and headed observations, field staff table (deficiencies shaded), shortcomings, locations table with *Nature of defect* and *Compliance* columns, patrolling chart check, signature block, footer with page numbers.
- **Export** to PDF, Word (.docx), Excel (.xlsx), HTML (print / save as PDF) and the **WhatsApp text** format; month / year exports of all notes, a **register** (one line per inspection with totals), and the **pending locations** list (spots reported more than once highlighted).
- **Import** reports from pasted WhatsApp messages (a whole exported chat works), **photos** (read on the device – Windows OCR / Google ML Kit on Android), Word, PDF, Excel, HTML and backups. Files saved by the app carry an exact copy of the report and come back unchanged.
- **Night duty lists** of patrolmen / keymen (WhatsApp text, PDF table, photo): beats with names, Home Guard marking and mobile numbers; *Who is on duty here?* for any KM or station, with **Call** and **WhatsApp** buttons.
- **Patrolling chart check** – the Winter Patrolling Chart 2026-27 (Kota – COR, KM 1 – 152.90, P-1&2 … P-37&38, rounds 23.00-01.20, 01.50-04.10, 04.40-07.00) is built in. From the departure and arrival times the app works out where the loco should have met every patrol pair and compares that with the staff recorded (as per chart / off position / not noticed); deviations can be added to the shortcomings. Further charts (monsoon etc.) can be added in the app.
- Locations register with compliance (attended on / action taken / under watch), backup file and a sync folder to keep Windows and Android in step.

## Download

Every successful build on `main` is published under **Releases**: `Footplate-Reports-Windows-x64.zip` (unzip, run `FootplateIR.App.exe`) and `Footplate-Reports-Android.apk`.

## Build

```
dotnet workload install maui
dotnet test tests/FootplateIR.Core.Tests/FootplateIR.Core.Tests.csproj
dotnet publish src/FootplateIR.App/FootplateIR.App.csproj -c Release -f net10.0-windows10.0.19041.0
dotnet publish src/FootplateIR.App/FootplateIR.App.csproj -c Release -f net10.0-android
```

Signing: add the repository secrets `WINDOWS_PFX_BASE64`, `WINDOWS_PFX_PASSWORD`, `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASSWORD` (see `build/`).

## Layout of the code

- `src/FootplateIR.Core` – data model, report / duty list reader, patrolling chart, PDF / Word / Excel / HTML writers, file import.
- `src/FootplateIR.App` – the Windows / Android app.
- `tests/FootplateIR.Core.Tests` – reads the sample WhatsApp reports, the two typed notes (`docs/samples`) and the duty lists, and checks every export.
