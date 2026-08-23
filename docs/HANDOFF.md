# Syssloappen - Project Handoff

Senast uppdaterad: 2026-08-23

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

Registrering loggar ännu inte in användaren automatiskt. Login och logout är nästa arbetsdel.

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

Det finns ännu inget automatiserat testprojekt. Tester för säkerhetskritisk Household-isolering bör läggas till när de första skyddade API-endpointsen byggs.

## Nästa arbetsdel

Nästa branch bör vara `feature/adult-authentication` och fokusera på US-002 och US-003:

1. Implementera login med ASP.NET Core Identity och cookie.
2. Lägg till ett skyddat endpoint, exempelvis `GET /api/auth/me`, som visar den autentiserade användarens ID, roll och HouseholdId.
3. Implementera logout.
4. Testa lyckad login, fel lösenord, skyddat endpoint före/efter login och logout.
5. Kontrollera att backend hämtar HouseholdId från den autentiserade användaren, aldrig från klientens request.

Implementera inte barn, sysslor eller Angular i samma branch.

## Kända kvarvarande saker

- Standardendpointet `WeatherForecast` från projektmallen finns fortfarande kvar och kan tas bort i en separat liten städändring.
- Ingen frontend finns ännu.
- Ingen e-postbekräftelse eller lösenordsåterställning ingår i MVP-arbetet ännu.
- Household-isolering är modellerad men kan först testas fullt ut när skyddade data-endpoints finns.
