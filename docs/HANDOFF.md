# Syssloappen - Project Handoff

Senast uppdaterad: 2026-08-24

Läs alltid `REQUIREMENTS.md` först. Den här filen kompletterar kraven med projektets aktuella tekniska status och fattade beslut.

## Aktuell status

Följande är implementerat och mergat till `main`:

- Tom monorepo-struktur med `frontend/`, `backend/` och `docs/`.
- ASP.NET Core Web API i `backend/Syssloappen.Api`.
- PostgreSQL-anslutning via Entity Framework Core och Npgsql.
- `Household`-modell med `Id`, `Name` och `CreatedAt`.
- ASP.NET Core Identity med en egen `ApplicationUser`.
- Varje `ApplicationUser` har ett obligatoriskt `HouseholdId`.
- Roller `Adult` och `Child` skapas automatiskt vid API-start.
- `POST /api/auth/register` skapar ett Household och ett Adult-konto i samma databastransaktion.
- Klienten kan inte välja `HouseholdId` eller roll vid registrering; backend sätter båda.
- Lösenord hanteras och hashas av ASP.NET Core Identity.
- `POST /api/auth/login` loggar in med ASP.NET Core Identity och en sessionscookie.
- `GET /api/auth/me` kräver inloggning och hämtar användar-ID, roll och `HouseholdId` från den autentiserade användaren.
- `POST /api/auth/logout` tar bort autentiseringscookien.
- Ett integrationstestprojekt använder en tillfällig SQLite-databas och testar login, fel lösenord, logout och två separata Households.

Adult authentication för US-002 och US-003 är mergad och pushad till `main`.

US-020 är implementerad och verifierad:

- `ChildProfile` har ett unikt ID, namn och obligatoriskt `HouseholdId`.
- `POST /api/children` låter endast en autentiserad Adult skapa barn.
- Requesten innehåller endast barnets namn. Backend hämtar alltid `HouseholdId` från den autentiserade användaren.
- `GET /api/children` visar endast barn i den autentiserade användarens Household.
- Integrationstester verifierar rollbehörighet, manipulerat `HouseholdId`, Household-isolering och att Adults i samma Household ser samma barn.

US-023 är implementerad, verifierad och mergad till `main`:

- `PUT /api/children/{id}` låter en autentiserad Adult ändra ett barns namn.
- Barnet hämtas med både Child-ID och den autentiserade användarens `HouseholdId` i samma databasfråga.
- Requesten innehåller endast namnet och kan inte ändra barnets Household.
- Tomma namn och namn över 100 tecken nekas utan att den befintliga informationen ändras.
- Integrationstester verifierar rollbehörighet, Household-isolering, manipulerade ID:n och att Adults i samma Household ser det nya namnet.

US-024 är implementerad, verifierad och mergad till `main`:

- `DELETE /api/children/{id}` låter endast en autentiserad Adult avaktivera ett barn.
- Barnet hämtas med Child-ID, den autentiserade användarens `HouseholdId` och aktiv status i samma databasfråga.
- Avaktivering sätter `IsActive` till `false`; databasraden raderas inte och kan därför behålla framtida historikrelationer.
- `GET /api/children` visar endast aktiva barn, och avaktiverade barn kan inte ändras via den aktiva barn-endpointen.
- Integrationstester verifierar rollbehörighet, soft delete, aktiv filtrering, Household-isolering och skydd mot manipulerade Child-ID:n.
- Kriterier som kräver framtida chores eller ett kopplat barnkonto är fortsatt okryssade och ska implementeras med respektive senare story.

Första delen av US-021 är implementerad och testad:

- Produktbeslutet är att alla nya barn alltid ska få ett konto direkt. Adult ska inte först skapa en fristående profil och därefter göra ett separat konto-anrop.
- `POST /api/children` tar barnets namn, ett barnvänligt användarnamn och lösenord och skapar `ChildProfile`, Identity-konto, profilkoppling och rollen `Child` i samma databastransaktion.
- Backend hämtar alltid `HouseholdId` från den autentiserade Adult-användaren. Klienten kan inte välja Household, roll, tekniskt Identity-användarnamn eller Child-ID.
- Barnets synliga användarnamn är skiftlägesokänsligt unikt inom Householdet men kan återanvändas i andra Households.
- Child-konton saknar e-post. Adult-kontons normaliserade e-post är fortsatt unik genom ett unikt databasindex.
- ASP.NET Core Identity hashar lösenordet. Ett internt, globalt unikt Identity-användarnamn genereras av backend och visas inte för barnet.
- Om kontoskapande, rolltilldelning eller profilkoppling misslyckas rullas hela transaktionen tillbaka.
- Det tidigare planerade separata konto-endpointet ingår inte längre i flödet.

Adult-styrd enhetskoppling är implementerad och testad:

- `POST /api/children/{childId}/pairing-codes` låter endast en autentiserad Adult skapa en kod för ett aktivt barn med kopplat konto i sitt eget Household.
- Backend genererar en kryptografiskt slumpmässig kod med åtta lättlästa tecken och tio minuters livslängd.
- Endast kodens SHA-256-hash lagras i databasen; klartexten returneras en gång till den vuxna.
- `POST /api/auth/child/pair` tar endast koden. Barnets enhet skickar varken `ChildId` eller `HouseholdId`.
- Backend härleder exakt Child, Identity-konto och Household från den hashade koden och verifierar aktiv status samt rollen `Child`.
- På mergad `main` markerar lyckad inlösen koden använd och autentiserar barnet med en vanlig, icke-beständig sessionscookie. Samma kod kan inte användas igen.
- Felaktig, utgången eller redan använd kod ger samma neutrala HTTP 401-svar.
- Inlösen begränsas till tio försök per minut och klientadress; ytterligare försök ger HTTP 429.
- Beständiga Child-enhetssessioner ingår i den färdiga sessionsdelen enligt avsnittet nedan.

Beständiga Child-enhetssessioner är implementerade och testade:

- Kodinlösen och skapande av `ChildDeviceSession` sker atomärt i samma databastransaktion.
- Sessionen binder ett sessions-ID och en hashad, kryptografiskt slumpmässig hemlighet till exakt `ChildProfile`, Identity-konto och `Household`.
- Klartexthemligheten finns endast i ASP.NET Core Identitys krypterade och signerade HttpOnly-cookie; databasen lagrar endast SHA-256-hashen.
- Cookien är beständig, `Secure`, `SameSite=Lax` och har en förnybar livslängd på sju dagar.
- Aktiv användning nära utgångstiden förnyar session och cookie, men aldrig förbi den absoluta maxlivslängden på trettio dagar.
- Cookievalideringen slår upp sessionen vid varje Child-anrop och kontrollerar hash, utgångstid, återkallelse, Child-roll, aktiv profil samt identiska konto-, Child- och Household-kopplingar.
- `GET /api/children/{childId}/device-sessions` låter en Adult se barnets kopplade sessioner i eget Household.
- `DELETE /api/children/{childId}/device-sessions/{sessionId}` låter en Adult återkalla en session i eget Household.
- Child-logout återkallar den aktuella databassessionen innan cookien tas bort.
- Avaktivering av ett barn återkallar alla barnets befintliga sessioner i samma databasändring; dessutom nekar den löpande backendvalideringen alltid en inaktiv profil.
- Alla Adult-endpoints kombinerar klientens Child-/sessions-ID med den autentiserade användarens `HouseholdId`, så manipulerade ID:n inte kan välja en annan familjs data.

På feature-branchen `feature/us-021-child-fallback-login` är säker reservinloggning implementerad och testad men ännu inte committad eller mergad:

- Varje nytt Household får en kryptografiskt slumpmässig, unik familjekod med tolv lättlästa tecken. Ett unikt databasindex skyddar även mot den osannolika händelsen att två genererade koder får samma SHA-256-hash.
- `POST /api/auth/child/login` tar endast familjekod, barnvänligt användarnamn och lösenord. DTO:n innehåller inga råa Household-, Child-, konto- eller rollfält; oväntade sådana JSON-fält kan inte styra backendens val.
- Backend hashar och normaliserar familjekoden, härleder Householdet från hashträffen och söker därefter det normaliserade, skiftlägesokänsliga barnanvändarnamnet endast inom detta Household.
- Lösenordet verifieras av ASP.NET Core Identity. Backend kräver dessutom rollen `Child`, en aktiv `ChildProfile` och identiska konto-, profil- och Household-kopplingar.
- Lyckad reservinloggning använder exakt samma gemensamma sessionsskapare som enhetskopplingen och får därför samma beständiga, maximalt tidsbegränsade och Adult-återkallningsbara `ChildDeviceSession`.
- Fel familjekod, användarnamn eller lösenord ger samma neutrala HTTP 401-svar. Försöken begränsas till tio per minut och klientadress; ett barnkonto låses inte av någon som känner till dess publika namn.
- Adult-login och befintlig enhetskoppling använder fortsatt sina tidigare endpoints och har särskilda regressionstester.

Familjekoden administreras med den minsta säkra lösningen:

- Klartextkoden returneras en gång när ett nytt Household registreras och en gång efter `POST /api/household/family-code/rotate`.
- `GET /api/household/family-code` visar endast om koden är konfigurerad, en maskerad kod med sista fyra tecken och senaste rotationstid. Endast en autentiserad `Adult` i Householdet får läsa status eller rotera.
- Databasen lagrar endast SHA-256-hash, sista fyra tecken och tidsstämpel, aldrig den fullständiga koden eller någon annan återanvändbar inloggningshemlighet.
- Säkerhetskonsekvensen är att en glömd kod inte kan återställas. En Adult måste rotera den, vilket omedelbart gör den gamla koden ogiltig. Familjekoden är bara en Household-identifierare; barnets Identity-hashade lösenord krävs alltid också.
- Befintliga Households får vid framtida migrering ett unikt hashat men avsiktligt oanvändbart värde. `FamilyCodeLastFour` lämnas tomt, reservlogin förblir avstängd och en Adult måste rotera en gång för att få en verklig kod. Detta undviker att lägga en klartexthemlighet i migrationen eller databasen.

## Teknik och versioner

- Node.js `22.23.2`
- npm `10.9.8`
- Angular CLI `22.1.5`
- .NET SDK `10.0.400`
- ASP.NET Core / target framework `net10.0`
- PostgreSQL `18`
- Microsoft Identity och EF Core `10.0.11`
- Npgsql Entity Framework Core provider `10.0.3`
- Lokalt `dotnet-ef`-verktyg `10.0.11` via `dotnet-tools.json`

Angular-projektet har ännu inte skapats. Frontendarbetet ska vänta tills den första backend-kärnan är stabil.

## Viktiga arkitekturbeslut

- En enkel monolit används: ett Angular-frontend, ett ASP.NET Core API och en PostgreSQL-databas.
- ASP.NET Core Identity används för konton, lösenord och roller.
- Cookie-baserad autentisering är vald för webbappen.
- Backend är auktoritativ för identitet, roll och Household-tillhörighet.
- Household-isolering får aldrig bygga på ID:n som klienten själv väljer.
- Barnets ordinarie enhet ska i första hand autentiseras genom en Adult-styrd, kortlivad engångskod som backend binder till exakt Child och Household.
- En lyckad enhetskoppling ska ge barnet en beständig men tidsbegränsad och återkallningsbar session, så att barnet normalt inte behöver logga in varje gång appen öppnas.
- Familjekod, barnvänligt användarnamn och lösenord ska finnas som reservinloggning. Barnets synliga användarnamn behöver bara vara unikt inom Householdet; ett separat tekniskt Identity-användarnamn kan säkerställa global unikhet internt.
- Alla nya barn ska skapas tillsammans med sitt Child-konto i ett enda Adult-initierat och atomärt backend-anrop.
- QR-skanning är en framtida användarvänlig presentation av samma engångskod och ska inte vara en separat autentiseringsmodell.
- Ingen microservice-, CQRS- eller annan extra arkitektur ska introduceras utan konkret behov.
- Arbeta i små feature-branches och merga endast gröna, testade delar till `main`.
- Uppdatera checkboxarna i `REQUIREMENTS.md` efter varje färdig, testad och mergad story.
- Markera endast helt uppfyllda kriterier med `[x]`; breda eller delvis uppfyllda kriterier ska förbli `[ ]`.

Den centrala säkerhetskedjan är:

```text
Authenticated User
        -> Role
        -> HouseholdId
        -> Authorization
        -> Allowed data
```

## Databas och lokal konfiguration

Den lokala utvecklingsdatabasen heter `syssloappen_dev`.

Anslutningssträngen ligger i .NET User Secrets under nyckeln:

```text
ConnectionStrings:SyssloappenDatabase
```

Hemligheten ligger utanför repot och får aldrig skrivas in i Git.

Aktuella migrationer:

- `InitialCreate` skapade tabellen `Households`.
- `AddIdentity` skapade Identity-tabellerna och kopplingen från `AspNetUsers.HouseholdId` till `Households.Id`.
- `AddChildProfiles` skapar tabellen `ChildProfiles` och dess koppling till `Households`.
- `AddChildProfileSoftDelete` lägger till `ChildProfiles.IsActive` med standardvärdet `true`.
- `AddChildAccounts` lägger till profilens konto-FK, Child-kontots synliga och normaliserade användarnamn samt unika index för profilkoppling, Household-användarnamn och Adult-e-post. Migrationen är genererad men ännu inte applicerad i `syssloappen_dev`.
- `AddChildPairingCodes` skapar tabellen `ChildPairingCodes` med hash, Child-, Household- och Adult-koppling, skapad tid, utgångstid och användningstid. Migrationen är genererad men ännu inte applicerad i `syssloappen_dev`.
- `AddChildDeviceSessions` skapar `ChildDeviceSessions` med Child-, konto- och Household-koppling, hashad sessionshemlighet, aktivitetstid, förnybar utgångstid, absolut maxgräns och återkallelsetid. Migrationen är genererad och granskad men inte applicerad i `syssloappen_dev`.
- `AddHouseholdFamilyCodes` lägger till unik familjekodshash, sista fyra tecken och rotationstid på `Households`. Den säkra backfillen för befintliga rader och det unika indexet är genererade och granskade men migrationen är inte applicerad i `syssloappen_dev`.

Vanliga kommandon från repots rot:

```bash
dotnet tool restore
dotnet restore backend/Syssloappen.Api/Syssloappen.Api.csproj
dotnet build backend/Syssloappen.Api/Syssloappen.Api.csproj
dotnet tool run dotnet-ef -- database update \
  --project backend/Syssloappen.Api/Syssloappen.Api.csproj \
  --startup-project backend/Syssloappen.Api/Syssloappen.Api.csproj
```

Starta API:t från `backend/Syssloappen.Api`:

```bash
dotnet run --launch-profile https
```

Lokala adresser från launch-profilen:

- HTTPS: `https://localhost:7216`
- HTTP: `http://localhost:5047`

## Genomförda tester

- API-projektet bygger med 0 fel och 0 varningar.
- EF Core rapporterar inga datamodellsändringar utan migration.
- `InitialCreate` och `AddIdentity` är tillämpade i `syssloappen_dev`.
- API-start skapar rollerna `Adult` och `Child`.
- Giltig vuxenregistrering returnerar HTTP 201 och skapar ett Household, en användare och Adult-rollkopplingen.
- Duplicerad e-post returnerar HTTP 400.
- Dublettförsöket lämnar inte kvar något extra Household, vilket verifierar transaktionen.
- Lösenordet lagras som Identity-hash, inte i klartext.

Lokal testdata finns i `syssloappen_dev`:

- Household: `Testfamiljen`
- Adult-konto: `adult@syssloappen.test`

Testlösenordet är avsiktligt inte dokumenterat här.

Integrationstesterna finns i `backend/Syssloappen.Api.Tests`.
Fyra auth-tester verifierar login, fel lösenord, skyddat endpoint före och efter logout, två separata Households samt att Adult-e-post fortsatt är skiftlägesokänsligt unik.
Fyra barn-endpointtester verifierar att en oinloggad användare och Child-rollen inte kan skapa barn, att klienten inte kan välja Household, att barn isoleras mellan Households och att Adults i samma Household kan se barnet.
Fem tester för US-021:s första del verifierar atomiskt profil- och kontoskapande, aktiv status, säker Identity-hash, Child-roll, backendstyrt Household, ignorerade manipulerade fält, användarnamnsunikhet inom Household, återanvändning mellan Households och rollback vid fel.
Sju enhetskopplingstester verifierar Adult- och Child-behörighet, aktiv status, Household-isolering, manipulerade Child-ID:n, hashad och kortlivad kod, exakt Child-autentisering, engångsanvändning, utgångstid och rate limiting.
Sju US-023-tester verifierar behörighet, lyckad namnändring, validering, Household-isolering och skydd mot manipulerade Child- och Household-ID:n.
Fyra US-024-tester verifierar behörighet, soft delete, aktiv filtrering, Household-isolering och skydd mot manipulerade Child-ID:n.
Alla 31 integrationstester är godkända i Release-konfiguration.

Elva integrationstester för sessionsdelen verifierar beständig cookie och hashad hemlighet, förnybar och absolut maximal livslängd, båda typerna av utgång, Adult-listning och återkallelse, logout, avaktiverat barn, Adult-behörighet, Household-isolering, manipulerade Child-/sessions-ID:n samt atomisk kodinlösen och sessionsskapande. Alla 42 integrationstester är godkända i Release-konfiguration.

Tolv integrationstester för reservinloggningen verifierar unik familjekod, hashad lagring, Household-härledning, skiftlägesokänsligt barnanvändarnamn, Identity-lösenord, neutrala felsvar, rate limiting, inaktiv profil, fel roll, brutna konto-/profil-/Household-kopplingar, Household-isolering, manipulerade ID-/rollfält, beständig och återkallningsbar session, Adult-behörighet, rotation samt regression för Adult-login och enhetskoppling. Alla 54 integrationstester är godkända i Release-konfiguration.

Release-build och formatteringskontroll är godkända utan fel eller varningar. EF Core rapporterar inga väntande modelländringar utanför migrationen. Ett idempotent PostgreSQL-script från `AddChildPairingCodes` till `AddChildDeviceSessions` har genererats och granskats: det skapar endast den nya sessionstabellen, främmande nycklar, index och migrationshistorikraden i en transaktion. Scriptet har inte körts mot databasen.

Ett idempotent PostgreSQL-script från `AddChildDeviceSessions` till `AddHouseholdFamilyCodes` har också genererats och granskats. Det lägger till de tre familjekodskolumnerna, backfyller befintliga Households utan klartexthemlighet, gör hashkolumnen obligatorisk, skapar det unika indexet och skriver migrationshistorikraden i en transaktion. Scriptet har inte körts mot databasen.

Migrationen `AddChildProfiles` är applicerad i `syssloappen_dev`. Ett manuellt HTTP-test mot PostgreSQL verifierade HTTP 401 utan login, lyckad skapning som Adult och isolering mellan två Households.
Ett manuellt US-023-test mot PostgreSQL verifierade lyckad namnändring i rätt Household, HTTP 404 från ett annat Household och fortsatt isolering i barnlistan.
Migrationen `AddChildProfileSoftDelete` är applicerad i `syssloappen_dev`; PostgreSQL lade till den obligatoriska `IsActive`-kolumnen med standardvärdet `true` utan fel.

## Aktuell arbetsdel

Första delen av US-021 är färdig och testad:

1. Adult skapar barnprofil och Child-konto tillsammans med `POST /api/children`.
2. Profil, konto, Household och Child-roll kopplas atomärt.
3. Barnvänligt användarnamn är skiftlägesokänsligt unikt inom Householdet men kan återanvändas i andra Households.
4. Child-konton saknar e-post och Adult-e-post förblir unik.
5. Automatiska integrationstester, Release-build, formatteringskontroll, EF-modellkontroll och genererad PostgreSQL-migrations-SQL är godkända.

Adult-styrd, kortlivad enhetskoppling är färdig och testad:

1. Endast Adult kan skapa kod för ett aktivt barn i eget Household.
2. Koden är kryptografiskt slumpmässig, kortlivad, hashad i databasen och kan bara användas en gång.
3. Barnets enhet löser in endast koden; backend bestämmer Child, konto och Household.
4. Lyckad inlösen autentiserar rätt Child och felaktiga försök begränsas.
5. Automatiska integrationstester, Release-build, formatteringskontroll, EF-modellkontroll och genererad PostgreSQL-migrations-SQL är godkända.

Den avgränsade US-021-delen med beständig Child-enhetssession, maximal livslängd, säker förnyelse, logout, Adult-återkallning och omedelbar backendkontroll av barnets aktiva status är färdig och testad.

Den avgränsade US-021-delen med reservinloggning via familjekod, barnvänligt användarnamn och Identity-lösenord är färdig och testad på feature-branchen. Migrationen och PostgreSQL-SQL är genererade och granskade men inte applicerade. Ändringarna inväntar uttryckligt godkännande före commit, push eller merge. QR, chores, poäng och approval-flöde ska fortfarande vänta.

## Kända kvarvarande saker

- Standardendpointet `WeatherForecast` från projektmallen finns fortfarande kvar och kan tas bort i en separat liten städändring.
- Ingen frontend finns ännu.
- Ingen e-postbekräftelse eller lösenordsåterställning ingår i MVP-arbetet ännu.
- ChildProfiles som skapades i utvecklingsdatabasen före enstegsflödet får inte automatiskt användarnamn och lösenord; de behöver hanteras eller återskapas när migrationen tas i bruk.
- Household-isolering är testad för barn-endpoints. Den måste fortfarande implementeras och testas separat för framtida sysslor och tilldelningar.
