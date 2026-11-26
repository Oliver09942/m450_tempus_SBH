# Tempus · Zeiterfassung

C#-Anwendung für das gemeinsame Testprojekt M450/M306. Tempus umfasst Arbeitszeit und Stempeluhr, datierte Teilzeitpensen, Gleitzeitkonten und Abwesenheiten. Das Personalbüro kann Monatsauswertungen exportieren und einfache Lohnblätter in CHF erstellen. Die Oberfläche läuft im Browser auf dem eigenen Rechner. SQLite speichert die Daten lokal.

## Start mit Visual Studio oder der Konsole

Zum Entwickeln werden das .NET 10 SDK und beim ersten Build Zugriff auf NuGet benötigt. Unter Windows **Start-Entwicklung.cmd** doppelklicken. Das Skript kopiert beim ersten Start die gemeinsame Ausgangsdatenbank nach **Daten** und öffnet die Anwendung im Standardbrowser.

In Visual Studio 2026 **Tempus.sln** öffnen und **Tempus.Web** als Startprojekt wählen. Für einen reproduzierbaren Lauf mit der gemeinsamen Ausgangsdatenbank im Terminal zunächst **Demodaten/tempus.sqlite3** nach **Daten/tempus.sqlite3** kopieren. Danach im Projektstamm starten:

```powershell
# PowerShell im Projektstamm, nach dem Kopieren der Ausgangsdatenbank
dotnet run --project Tempus.Web -- --Port 5184 --DataDir (Resolve-Path ./Daten).Path
```

Die lokale Adresse lautet **http://127.0.0.1:5184**. Ohne expliziten Port wählt die App einen freien Port. Der direkte Start ohne Datenpfad verwendet den lokalen Benutzerordner und erstellt bei Bedarf eigene Demodaten.

| Benutzername | Rolle |
| --- | --- |
| nora | Mitarbeit, 100 % |
| luca | Mitarbeit, 60 % |
| samira | Projektleitung Kundenportal |
| mara | Projektleitung Betrieb & Support und Interne Weiterbildung |
| jonas | Entwicklungsleitung |
| team01 bis team43 | Mitarbeit |
| hr | Personalbüro |
| admin | Administration |

Gemeinsames Demopasswort: **Tempus-Demo26!**. Die mitgelieferte Datenbank enthält 50 fiktive Personen, drei Projekte und 1896 Arbeitsbuchungen. Die Projektleitungen sehen die Monatsstunden ihrer Projekte. Die Entwicklungsleitung erhält die Gesamtsicht über alle Projekte. Für einen neuen Durchlauf die App beenden, den eigenen Datenordner umbenennen und die Ausgangsdatenbank erneut kopieren.

## Projektstruktur

| Pfad | Inhalt |
| --- | --- |
| Tempus.Core | Isoliert testbare Berechnung von Arbeitszeit, Gleitzeit und Lohn |
| Tempus.Web | HTTP-Endpunkte, Rollen, Datenzugriff und Weboberfläche in wwwroot |
| Demodaten | Gemeinsamer Ausgangsstand für die Gruppen |
| Verteilung | Startdatei und Bedienungsanleitung für die portable Windows-Version sowie Paketskript |

## Windows-Paket für die Gruppen erstellen

In PowerShell im Projektstamm ausführen:

```powershell
./Verteilung/Paket-bauen.ps1
```

Voraussetzungen auf dem Build-Rechner sind Git und das .NET 10 SDK. Das ZIP unter **artifacts/Tempus_Windows_und_Quellcode.zip** enthält die Windows-x64-Anwendung samt Laufzeit, Quellcode, Startdatei, Anleitung und Demodaten. Auf dem Zielrechner das ZIP vollständig entpacken und **Start-Tempus.cmd** ausführen. Das ZIP lässt sich als Download an ein Git-Release anhängen.

Der optionale Gerätezugriff über HTTPS verwendet eine gemeinsame Datenbank auf dem laufenden Gruppenrechner. Einrichtung, Rollenrechte und die fachlichen Regeln für Tagesbilanzen, Abwesenheiten, Monatsabschluss und Lohnblatt stehen in der [Bedienungsanleitung](Verteilung/LIESMICH.md).
