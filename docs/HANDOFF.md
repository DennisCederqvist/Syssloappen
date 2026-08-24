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
- Ingen microservice-, CQRS- eller annan extra arkitektur ska introduceras utan konkret behov.
- Arbeta i små feature-branches och merga endast gröna, testade delar till `main`.

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
Tre auth-tester verifierar login, fel lösenord, skyddat endpoint före och efter logout samt att två Adults i olika Households identifieras med rätt `HouseholdId`.
Fyra US-020-tester verifierar att en oinloggad användare och Child-rollen inte kan skapa barn, att klienten inte kan välja Household, att barn isoleras mellan Households och att Adults i samma Household kan se barnet.
Sju US-023-tester verifierar behörighet, lyckad namnändring, validering, Household-isolering och skydd mot manipulerade Child- och Household-ID:n.
Alla fjorton integrationstester är godkända.

Migrationen `AddChildProfiles` är applicerad i `syssloappen_dev`. Ett manuellt HTTP-test mot PostgreSQL verifierade HTTP 401 utan login, lyckad skapning som Adult och isolering mellan två Households.
Ett manuellt US-023-test mot PostgreSQL verifierade lyckad namnändring i rätt Household, HTTP 404 från ett annat Household och fortsatt isolering i barnlistan.

## Aktuell arbetsdel

US-023 omfattar följande färdiga delar:

1. Adult kan ändra ett barns namn med `PUT /api/children/{id}`.
2. Endast barn i den autentiserade användarens Household kan hämtas för ändring.
3. Klienten kan inte ändra barnets `HouseholdId`.
4. Automatiska integrationstester och ett manuellt PostgreSQL-test är godkända.
5. Household-isoleringen är granskad och verifierad före merge.

US-023 är färdig. Nästa arbetsdel ska bekräftas innan en ny feature-branch skapas. Enligt den prioriterade ordningen är nästa planerade story US-024, avaktivering av barn med bevarad historik. Barnets login, sysslor, poäng och godkännandeflödet ingår inte i US-023.

## Kända kvarvarande saker

- Standardendpointet `WeatherForecast` från projektmallen finns fortfarande kvar och kan tas bort i en separat liten städändring.
- Ingen frontend finns ännu.
- Ingen e-postbekräftelse eller lösenordsåterställning ingår i MVP-arbetet ännu.
- Household-isolering är testad för barn-endpoints. Den måste fortfarande implementeras och testas separat för framtida sysslor och tilldelningar.
