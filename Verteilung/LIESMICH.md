# Tempus · Lokale Zeiterfassung
Version 1.1.0 · Unterrichtsausgabe M450/M306 · 26. November 2026

## In einer Minute starten

1. Das ganze ZIP in einen beschreibbaren Ordner auf dem Rechner entpacken. Jede Gruppe verwendet einen eigenen Ordner.
2. Unter Windows x64 **Start-Tempus.cmd** doppelklicken. Das Konsolenfenster offen lassen. Der Standardbrowser öffnet die lokale Anwendung. Bei Bedarf die im Fenster angezeigte Adresse in Chrome oder Edge kopieren.
3. Mit **nora** und **Tempus-Demo26!** anmelden. Unter **Arbeitszeiten** einen Tag und ein Projekt auswählen, Beginn, Ende und Pause erfassen. In der **Monatsauswertung** erscheinen die Buchung, ihr Einfluss auf die Tagesdifferenz und der neue Gleitzeitsaldo.
4. Über **Abmelden** die Rolle wechseln. Zum Beenden im Konsolenfenster Strg+C drücken.

Die Windows-Ausgabe enthält die .NET-Laufzeit, SQLite und sämtliche Browserdateien. Sie läuft offline. Beim normalen Start lauscht der eingebettete Webprozess ausschliesslich auf der lokalen Loopback-Adresse. Eine Serverinstallation, ein Datenbankdienst und Administratorrechte sind für den normalen Start nicht erforderlich.

## Demozugänge

Das gemeinsame Anfangspasswort lautet **Tempus-Demo26!**. Jedes Konto kann sein Passwort ändern.

| Benutzername | Rolle | Was sich damit ausprobieren lässt |
| --- | --- | --- |
| nora | Mitarbeit, 100 % | Eigene Arbeitszeiten, Stempeluhr, Abwesenheiten und Monatsauswertung |
| luca | Mitarbeit, 60 % | Acht Stunden Sollzeit am Montag, Dienstag und Donnerstag, andere Wochentage frei |
| samira | Projektleitung | Kundenportal: Projektstunden aller Beteiligten |
| mara | Projektleitung | Betrieb & Support und Interne Weiterbildung |
| jonas | Entwicklungsleitung | Alle Projekte, Beteiligte, Projekt- und Gesamtsummen |
| team01 bis team43 | Mitarbeit | Persönliche Zeiterfassung mit verschiedenen Wochenplänen |
| hr | Personalbüro | Abwesenheiten bearbeiten, Zeitmonate abschliessen, Stundenliste, CSV und Lohnblatt |
| admin | Administration | Konten, Projekte, Pensen, Monatsabschlüsse und HR-Funktionen |

Die Ausgangsdaten enthalten 50 fiktive Personen und drei Projekte. Jede angemeldete Person kann eigene Zeiten auf die Projekte buchen. Unter **Projektstunden** sieht eine Projektleitung die Summe ihrer zugewiesenen Projekte sowie die Beiträge der einzelnen Personen im gewählten Monat. Die Entwicklungsleitung und die Administration sehen alle Projekte, auch solche ohne Buchungen. Eine Korrektur wirkt sich beim nächsten Aufruf der Auswertung auf die Projekt- und Gesamtsumme aus.

Nur HR und Administration haben Zugriff auf fremde Journale, Abwesenheiten und Lohnblätter. HR kann fremde Anträge bearbeiten und fremde Monate abschliessen oder wieder öffnen. Die Administration übernimmt dies bei eigenen HR-Anträgen. Die Leitungsrollen lesen Projektstunden ohne Abwesenheitsgründe, Personaljournal oder Lohndaten anderer Personen. Die Administration kann unter **Verwaltung → Projekte → Leitung zuweisen** die zuständige Projektleitung setzen, wechseln oder entfernen. Die Zuordnung gilt sofort für alle abrufbaren Monate.

Beim Öffnen einer älteren Datenbank werden vorhandene Konten der Rolle Teamleitung auf Projektleitung umgestellt. Die Administration weist ihre Projekte einmalig zu. Bestehende Buchungen und persönliche Passwörter bleiben erhalten.

## Daten und reproduzierbare Testläufe

**Demodaten/tempus.sqlite3** enthält einen einheitlichen Ausgangsstand für alle Gruppen. Die fiktiven Arbeitsbuchungen reichen vom 1. Oktober bis 25. November 2026. Die Starterdatei kopiert den Ausgangsstand beim ersten Start nach **Daten/tempus.sqlite3**. Alle weiteren Änderungen landen in dieser Arbeitskopie. Das Programm verwendet Datum und lokale Uhrzeit des Rechners für den aktuellen Stichtag.

Für eine Sicherung die App beenden und den gesamten Ordner **Daten** kopieren. Für einen neuen Testlauf die App beenden und **Daten** umbenennen, beispielsweise in **Daten-Lauf-1**. Beim nächsten Start entsteht wieder eine Kopie des Ausgangsstands. Ein direkt gestartetes **Programm/Tempus.exe** verwendet standardmässig den Benutzerordner **%LOCALAPPDATA%/TempusM450** und erzeugt bei einer neuen Datenbank Beispieldaten relativ zum Starttag.

## Gemeinsamer Datenstand im Büro, im Homeoffice und unterwegs

Ein laufender Tempus-Rechner kann den Browsern anderer Geräte dieselbe SQLite-Datenbank über HTTPS bereitstellen. Die Datenbankdatei bleibt auf diesem Rechner. Alle Geräte buchen auf dieselbe Instanz. Im Homeoffice oder unterwegs muss die Adresse über das Schul- oder Firmennetz (zum Beispiel per VPN) erreichbar sein. Auf einem einzelnen Notebook funktioniert der lokale Start auch offline.

1. Tempus auf dem Gruppenrechner lokal starten. Für die vorgesehenen Gerätebenutzer eigene Konten und Passwörter anlegen oder ihr Anfangspasswort lokal ändern. Bekannte Demopasswörter sind für die HTTPS-Anmeldung gesperrt.
2. Für die Freigabe eine konkrete IPv4-Adresse des Rechners und einen erreichbaren Namen oder eine IP-Adresse wählen. Ein vom Client als vertrauenswürdig eingestuftes Zertifikat muss zu diesem Namen bzw. dieser IP-Adresse passen. Die PFX-Datei mit privatem Schlüssel bleibt auf dem Gruppenrechner. Der TCP-Port muss von den vorgesehenen Geräten erreichbar sein.
3. Den lokalen Prozess beenden. In PowerShell im entpackten Windows-Ordner starten. In diesem Beispiel die IP-Adresse, den Hostnamen und den Zertifikatspfad an die eigene Umgebung anpassen. Für eine unverschlüsselte PFX-Datei entfällt die Eingabe der Passphrase.

```powershell
$secret = Read-Host 'PFX-Passphrase' -AsSecureString
$env:TEMPUS_PFX_PASSWORT = [Net.NetworkCredential]::new('', $secret).Password
try {
    .\Start-Tempus.cmd --FreigabeIP 192.168.1.20 --FreigabeUrl https://tempus.schule.example:8443 --FreigabePfx "C:\Zertifikate\tempus.pfx"
} finally {
    Remove-Item Env:\TEMPUS_PFX_PASSWORT -ErrorAction SilentlyContinue
}
```

4. Auf dem zweiten Gerät die ausgegebene HTTPS-Adresse öffnen und mit dem persönlichen Konto anmelden. Auf dem Gruppenrechner funktioniert die lokale Browseradresse weiterhin. Beim Beenden endet auch die Netzwerkfreigabe. Mehrere unabhängige lokale Kopien sind getrennte Datenbestände.

## Was die App berechnet

- **Arbeitszeit** in ganzen Minuten. Nettozeit ist Ende minus Beginn minus Pause. Mehrere Buchungen pro Tag sind möglich. Überschneidungen, unplausible Pausen und ein Ende in der Zukunft werden geprüft. 24:00 ist als Tagesende zulässig. Manuelle Nachtschichten auf zwei Tage verteilen. Die Stempeluhr teilt beim Stoppen über Mitternacht automatisch auf. Pausen über Mitternacht danach am betreffenden Tag nachtragen.
- **Teilzeit** über einen datierten Wochenplan mit sieben Tageswerten. Die Referenzwoche für 100 % beträgt 40 Stunden. Eine Änderung gilt ab ihrem Gültigkeitsdatum bis zum nächsten Plan. Erfasste Arbeit an einem planfreien Tag erhöht den Gleitzeitsaldo.
- **Gleitzeit** als Vortrag plus Arbeitszeit plus bewilligte Gutschriften minus Sollzeit. Der laufende Monat berücksichtigt die volle Sollzeit des heutigen Tages. Zukünftige Kalendertage zählen erst, wenn sie erreicht sind. Die Vorschau auf das Soll des gesamten Monats ist separat ausgewiesen.
- **Abwesenheiten** gelten ganztägig und einschliesslich des Enddatums. Bewilligte Ferien und Krankheit schreiben die geplanten Minuten gut. Unbezahlter Urlaub und Sabbatical reduzieren das Soll auf null. Bei Gleitzeitabbau bleibt das Soll bestehen. Die Geschäftsregeln für diese Unterrichtsausgabe verwenden den hinterlegten Wochenplan ohne separaten Feiertagskalender.
- **Statistiken** zeigen die Stunden nach Projekt, den Saldoverlauf und die einzelnen Tageswerte. Der persönliche CSV-Export gibt die Minutenwerte des gewählten Monats aus.
- **Monatsabschluss** durch HR oder Administration. Vollständig vergangene Monate werden je Person in chronologischer Reihenfolge geschlossen. Offene Anträge oder eine laufende Stempeluhr können den Abschluss verhindern. Abgeschlossene Monate sperren Korrekturen. Eine Wiederöffnung erfolgt vom jüngsten Abschluss aus.

## Personalbüro und Lohnblatt

1. Mit **hr** anmelden und **Personalbüro** wählen. Über **Monat** den Berichtszeitraum festlegen. Die Stundenliste enthält für alle im Zeitraum eingetretenen Personen Arbeitszeit, Soll, Gutschriften, unbezahlte Stunden, Monatsdifferenz, Saldo und Abschlussstatus. Sie lässt sich drucken oder als CSV für Excel exportieren.
2. Über **Person** die gewünschte Person wählen und **Lohnblatt erstellen** öffnen. Arbeitgeber und Modell eingeben. Beim **Stundenlohn** multipliziert die App den Ansatz mit der Summe aus Arbeit und bezahlten Gutschriften. Sie rechnet intern mit Minuten und rundet den Grundlohn einmal auf Rappen.
3. Beim **Monatslohn** den für das tatsächliche Pensum vereinbarten Monatsbetrag eintragen. HR erfasst allfällige Korrekturen für Eintritte und unbezahlte Abwesenheiten in CHF. Zulagen und Korrekturen ergeben mit dem Grundlohn den Bruttolohn. Sozialbeiträge, Pensionskasse, Versicherung, Quellensteuer und weitere Abzüge werden als Beträge eingegeben. Die Anwendung berechnet daraus den Auszahlungsbetrag. Sie berechnet keine gesetzlichen Tarife oder individuellen Beitragssätze.
4. Als **Entwurf** speichern. Änderungen an den Stunden werden beim erneuten Speichern des Lohnblatts berücksichtigt. Ein **definitives** Lohnblatt setzt einen abgeschlossenen Zeitmonat voraus. Es bleibt als gespeicherter Stand erhalten. HR kann es wieder öffnen und danach aktualisieren. Über **Lohnblatt drucken / PDF** im Browser den Drucker oder «Als PDF speichern» wählen.

## Quellcode und eigene Tests

**Quellcode/Tempus.sln** in Visual Studio 2026 mit .NET 10 SDK und der Workload «ASP.NET und Webentwicklung» öffnen. **Tempus.Web** als Startprojekt auswählen. Zum erstmaligen Wiederherstellen der NuGet-Pakete ist eine Verbindung zu NuGet erforderlich. Für die mitgelieferte Windows-App entfällt dieser Schritt.

- **Tempus.Core** enthält die unabhängig testbaren Regeln für Arbeitszeiten, Tagesbilanzen und Lohnbeträge.
- **Tempus.Web** enthält HTTP-Endpunkte, Rollenprüfung, SQLite-Persistenz und die lokale Oberfläche unter **wwwroot**.
- Über die Konsole im Ordner **Quellcode** starten. Das funktioniert auch unter macOS oder Linux mit .NET 10 SDK. Die fertige Windows-Ausgabe und der Start aus dem Quellcode verwenden denselben Anwendungscode.

```powershell
New-Item -ItemType Directory -Force ./Testdaten | Out-Null
$daten = (Resolve-Path ./Testdaten).Path
dotnet run --project Tempus.Web -- --Port 5184 --DataDir "$daten" --NoBrowser true
```

Im Browser **http://127.0.0.1:5184** öffnen. Der feste Port und ein eigener Datenordner eignen sich für Selenium- oder Integrationstests. Für exakt die mitgelieferte Ausgangslage vor dem ersten Start die Datei **Demodaten/tempus.sqlite3** in den gewählten Testdatenordner kopieren. Für Isolation pro Testsuite einen eigenen Ordner verwenden.
