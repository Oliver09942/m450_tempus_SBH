# Tempus · Local Time Tracking
Version 1.1.0 · Classroom edition M450/M306 · 26 November 2026

Note: the application's user interface is in German. UI labels are kept in German below (bold) with an English gloss where helpful.

## Get started in one minute

1. Extract the whole ZIP into a writable folder on your computer. Each group uses its own folder.
2. On Windows x64, double-click **Start-Tempus.cmd**. Keep the console window open. Your default browser opens the local application. If needed, copy the address shown in the window into Chrome or Edge.
3. Sign in with **nora** and **Tempus-Demo26!**. Under **Arbeitszeiten** (working hours), choose a day and a project, then enter start, end and break. The **Monatsauswertung** (monthly report) shows the entry, its effect on the daily difference and the new flextime balance.
4. Use **Abmelden** (sign out) to switch roles. To quit, press Ctrl+C in the console window.

The Windows build includes the .NET runtime, SQLite and all browser files. It runs offline. On a normal start the embedded web process listens only on the local loopback address. No server installation, database service or administrator rights are needed for a normal start.

## Demo accounts

The shared initial password is **Tempus-Demo26!**. Every account can change its password.

| Username | Role | What to try |
| --- | --- | --- |
| nora | Employee, 100 % | Own working hours, time clock, absences and monthly report |
| luca | Employee, 60 % | Eight hours of target time on Monday, Tuesday and Thursday, other weekdays off |
| samira | Project lead | Kundenportal: project hours of all participants |
| mara | Project lead | Betrieb & Support and Interne Weiterbildung |
| jonas | Development lead | All projects, participants, project and overall totals |
| team01 to team43 | Employee | Personal time tracking with different weekly schedules |
| hr | HR office | Edit absences, close time months, hours list, CSV and payslip |
| admin | Administration | Accounts, projects, schedules, month closings and HR functions |

The seed data contains 50 fictitious people and three projects. Every signed-in person can book their own time to the projects. Under **Projektstunden** (project hours), a project lead sees the total for their assigned projects and the contributions of each person in the selected month. The development lead and administration see all projects, including those without bookings. A correction affects the project and overall totals the next time the report is opened.

Only HR and administration can access other people's journals, absences and payslips. HR can process other people's requests and close or reopen other people's months. Administration takes over this for HR's own requests. The lead roles read project hours without absence reasons, personnel journals or payroll data of other people. Under **Verwaltung → Projekte → Leitung zuweisen** (administration → projects → assign lead), administration can set, change or remove the responsible project lead. The assignment applies immediately to all months that can be retrieved.

When an older database is opened, existing accounts with the team lead role are switched to project lead. Administration then assigns their projects once. Existing bookings and personal passwords are kept.

## Data and reproducible test runs

**Demodaten/tempus.sqlite3** contains a uniform starting state for all groups. The fictitious time entries run from 1 October to 25 November 2026. The launcher copies the starting state to **Daten/tempus.sqlite3** on first start. All further changes go into this working copy. The program uses the computer's date and local time as the current reference day.

To back up, quit the app and copy the whole **Daten** folder. For a new test run, quit the app and rename **Daten**, for example to **Daten-Lauf-1**. The next start creates a fresh copy of the starting state. A directly started **Programm/Tempus.exe** uses the user folder **%LOCALAPPDATA%/TempusM450** by default and creates sample data relative to the start day for a new database.

## Shared data across office, home office and on the road

A running Tempus machine can serve the same SQLite database to the browsers of other devices over HTTPS. The database file stays on that machine. All devices book into the same instance. From home or on the road, the address must be reachable through the school or company network (for example via VPN). On a single laptop the local start also works offline.

1. Start Tempus locally on the group machine. Create separate accounts and passwords for the intended device users, or change their initial passwords locally. Known demo passwords are blocked for HTTPS sign-in.
2. For sharing, choose a concrete IPv4 address of the machine and a reachable name or IP address. A certificate trusted by the client must match this name or IP address. The PFX file with the private key stays on the group machine. The TCP port must be reachable from the intended devices.
3. Quit the local process. Start from PowerShell in the extracted Windows folder. In this example, adjust the IP address, host name and certificate path to your environment. For an unencrypted PFX file, the passphrase prompt is not needed.

```powershell
$secret = Read-Host 'PFX passphrase' -AsSecureString
$env:TEMPUS_PFX_PASSWORT = [Net.NetworkCredential]::new('', $secret).Password
try {
    .\Start-Tempus.cmd --FreigabeIP 192.168.1.20 --FreigabeUrl https://tempus.schule.example:8443 --FreigabePfx "C:\Zertifikate\tempus.pfx"
} finally {
    Remove-Item Env:\TEMPUS_PFX_PASSWORT -ErrorAction SilentlyContinue
}
```

4. On the second device, open the HTTPS address that was printed and sign in with your personal account. The local browser address keeps working on the group machine. When the app exits, the network sharing ends too. Several independent local copies are separate data sets.

## What the app calculates

- **Working time** in whole minutes. Net time is end minus start minus break. Several entries per day are possible. Overlaps, implausible breaks and an end in the future are checked. 24:00 is allowed as the end of day. Split manual night shifts across two days. The time clock splits automatically at midnight when stopped. Add breaks across midnight afterwards on the respective day.
- **Part-time** via a dated weekly schedule with seven daily values. The reference week for 100 % is 40 hours. A change applies from its effective date until the next schedule. Work recorded on a day off increases the flextime balance.
- **Flextime** as carry-over plus working time plus approved credits minus target time. The current month counts the full target time of today. Future calendar days count only once they are reached. The preview of the whole month's target is shown separately.
- **Absences** are full-day and inclusive of the end date. Approved vacation and sick leave credit the planned minutes. Unpaid leave and sabbatical reduce the target to zero. When taking flextime off, the target remains. The business rules of this classroom edition use the stored weekly schedule without a separate public-holiday calendar.
- **Statistics** show hours by project, the balance trend and the individual daily values. The personal CSV export outputs the minute values of the selected month.
- **Month closing** by HR or administration. Fully elapsed months are closed per person in chronological order. Open requests or a running time clock can prevent closing. Closed months lock corrections. Reopening starts from the most recent closing.

## HR office and payslip

1. Sign in as **hr** and choose **Personalbüro** (HR office). Use **Monat** (month) to set the reporting period. The hours list contains, for everyone who joined within the period, working time, target, credits, unpaid hours, monthly difference, balance and closing status. It can be printed or exported as CSV for Excel.
2. Use **Person** to choose the person and open **Lohnblatt erstellen** (create payslip). Enter the employer and the model. For the **hourly wage**, the app multiplies the rate by the sum of work and paid credits. It calculates internally in minutes and rounds the base pay to the cent once.
3. For the **monthly salary**, enter the monthly amount agreed for the actual workload. HR enters any corrections for joiners and unpaid absences in CHF. Allowances and corrections plus the base pay give the gross pay. Social contributions, pension fund, insurance, withholding tax and other deductions are entered as amounts. The application calculates the net payout from them. It does not calculate statutory rates or individual contribution rates.
4. Save as a **draft**. Changes to the hours are taken into account when the payslip is saved again. A **final** payslip requires a closed time month. It remains as a stored state. HR can reopen and then update it. Use **Lohnblatt drucken / PDF** (print payslip / PDF) to choose a printer or "Save as PDF" in the browser.

## Source code and your own tests

Open **Quellcode/Tempus.sln** in Visual Studio 2026 with the .NET 10 SDK and the "ASP.NET and web development" workload. Select **Tempus.Web** as the startup project. Restoring the NuGet packages for the first time requires a connection to NuGet. This step is not needed for the bundled Windows app.

- **Tempus.Core** contains the independently testable rules for working hours, daily balances and payroll amounts.
- **Tempus.Web** contains the HTTP endpoints, role checks, SQLite persistence and the local UI under **wwwroot**.
- Start from the console in the **Quellcode** folder. This also works on macOS or Linux with the .NET 10 SDK. The finished Windows build and a start from source use the same application code. With Docker, `docker compose up --build` in the source folder does the same.

```powershell
New-Item -ItemType Directory -Force ./Testdaten | Out-Null
$daten = (Resolve-Path ./Testdaten).Path
dotnet run --project Tempus.Web -- --Port 5184 --DataDir "$daten" --NoBrowser true
```

Open **http://127.0.0.1:5184** in the browser. The fixed port and a separate data folder suit Selenium or integration tests. For exactly the bundled starting state, copy **Demodaten/tempus.sqlite3** into the chosen test data folder before the first start. Use a separate folder for each test suite to keep them isolated.
