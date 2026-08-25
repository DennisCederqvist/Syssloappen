# Syssloappen - Project Handoff

Senast uppdaterad: 2026-08-25

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

Säker reservinloggning är implementerad, testad och mergad till `main`:

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

US-030 är implementerad, committad och testad:

- `POST /api/chores` låter endast en autentiserad Adult skapa en syssla med titel och valfri beskrivning.
- Backend hämtar alltid `HouseholdId` och skapande konto från den autentiserade användaren. Oväntade `HouseholdId`, `CreatedByUserId` eller tidsfält i JSON kan inte styra de lagrade värdena.
- Titel och beskrivning trimmas och begränsas till 100 respektive 500 tecken. En tom titel nekas utan att en databasrad lämnas kvar.
- `GET /api/chores` returnerar endast sysslor vars `HouseholdId` matchar den autentiserade Adult-användaren. Alla Adults i samma Household ser samma lista, medan andra Households förblir isolerade.
- `Chore.CreatedByUserId` sparar vilket Identity-konto som skapade sysslan.

Den avgränsade backenddelen av US-031 är implementerad, committad och testad:

- `POST /api/chore-assignments` låter endast en autentiserad Adult tilldela en befintlig syssla till ett aktivt barn.
- Requesten innehåller endast `ChoreId` och `ChildId`. Backend hämtar alltid `HouseholdId`, tilldelande Adult-konto och UTC-tid från den autentiserade användaren och serverns `TimeProvider`.
- Både sysslan och barnet söks med den autentiserade Adult-användarens `HouseholdId`; ett främmande, manipulerat eller obefintligt ID ger HTTP 404 och skapar ingen tilldelning.
- Noll och negativa ID:n ger HTTP 400. Oväntade ID-, Household-, ägar-, roll- och tidsfält kan inte styra den lagrade raden.
- `ChoreAssignment` sparar `HouseholdId`, `ChoreId`, `ChildId`, `AssignedByUserId` och `AssignedAt`. Ingen status, completion, approval, poäng-, QR- eller frontendfunktion har lagts till.

Den avgränsade Child-vyn är implementerad, committad och testad:

- `GET /api/child/chore-assignments` kräver en autentiserad användare med rollen `Child`; anonyma användare får HTTP 401 och Adults HTTP 403.
- Endpointen tar inget `ChildId`, `HouseholdId` eller annat ägarfält. Backend härleder kontot från den validerade cookien och hittar den aktiva barnprofil vars konto- och Household-koppling matchar.
- SQL-frågan kräver samma autentiserade konto, ChildProfile och Household på tilldelningen samt samma Household på sysslan. Syskons, andra familjers och inkonsekventa tilldelningsrader returneras inte.
- Svaret innehåller endast tilldelnings-ID, sysslo-ID, titel, valfri beskrivning och tilldelningstid. Nyaste tilldelningar visas först.
- Både Adult-styrd enhetskoppling och reservinloggning ger åtkomst till samma privata vy. Avaktivering gör sessionen ogiltig medan den historiska tilldelningsraden bevaras.
- Själva US-040-delen ändrade ingen datamodell. Status och rapportering läggs till i den efterföljande US-041-delen nedan; completion, approval och frontend väntar fortfarande.

US-041:s avgränsade rapporteringsdel är implementerad, testad och mergad till `main`:

- `POST /api/child/chore-assignments/{assignmentId}/submit` kräver en autentiserad användare med rollen `Child`; anonyma användare får HTTP 401 och Adults HTTP 403.
- Endpointen har ingen request-DTO. Klienten väljer därför inte Child, Household, ägare, status eller tid. Backend härleder konto och Household från den validerade Identity-cookien, hittar den aktiva barnprofilen och använder serverns `TimeProvider`.
- Tilldelningen hämtas med tilldelnings-ID, autentiserat barn, konto, aktiv status och samma Household på tilldelning, barn och syssla i samma SQL-fråga. Syskons, andra Households, obefintliga och inkonsekventa rader ger neutralt HTTP 404.
- Ett positivt ID krävs. Noll och negativa ID:n ger HTTP 400. Oväntade JSON-fält ignoreras eftersom endpointen inte binder någon request body och kan inte styra den sparade raden.
- En egen tilldelning i läget `Assigned` ändras till `PendingApproval` och får `SubmittedAt` satt till aktuell UTC-tid i backend. Barn- och sysslekopplingarna bevaras på tilldelningen.
- Upprepad rapportering ger tydligt HTTP 409 och ändrar inte den första rapporteringstiden. `Status` är ett EF Core-concurrency token så två samtidiga uppdateringar inte båda kan lyckas.
- Child-listningen visar nu `Status` och nullable `SubmittedAt`. Den visar både `Assigned` och `PendingApproval`; det breda kriteriet för `NeedsRedo` och `Approved` väntar på Adult-flödet.
- Ingen completion eller poäng skapas. Approval, rejection, frontend och QR ingår inte i arbetsdelen.

Adult-granskning och poäng är implementerade, testade och mergade till `main`:

- `POST /api/chores` tar nu valfritt `Points`. Backend använder `5` som standard och accepterar endast `5`, `10`, `15` eller `20`; samma begränsning finns som check constraint i databasen.
- `ChoreAssignment.Points` är en snapshot av sysslans poäng när tilldelningen skapas. Manipulerade poäng-, status- och ägarfält i tilldelningsrequesten kan inte ändra värdet.
- `GET /api/chore-assignments` låter en Adult se Householdets tilldelningar med barn, syssla, poäng, status, rapporteringstid, granskare, granskningstid och kommentar. SQL-frågan kräver samma Household på tilldelning, barn och syssla.
- `POST /api/chore-assignments/{assignmentId}/approve` och `/reject` accepterar endast en valfri kommentar. Backend bestämmer Adult, Household, status, tid och utdelade poäng.
- Endast `PendingApproval` kan granskas. Godkännande sätter `Approved` och skapar atomärt exakt en `ChoreCompletion`; nekande sätter `NeedsRedo` utan completion eller poäng.
- Ett barn kan rapportera en `NeedsRedo`-tilldelning igen. Den gamla granskningsinformationen rensas då och status återgår till `PendingApproval` med en ny backendtid.
- Varje completion sparar Household, tilldelning, barn, syssla, godkännande Adult, UTC-tid och de utdelade snapshot-poängen. Ett unikt index på `AssignmentId` skyddar dessutom mot dubbel utdelning.
- `GET /api/child/points` summerar endast det autentiserade aktiva barnets egna, Household-konsistenta completions. Child-listningen visar tilldelningens poäng, alla fyra statuslägen och eventuell omarbetningskommentar.
- Frontend är inte skapad. Den beslutade framtida vyn är en titelruta och en poängrullista med `5`, `10`, `15`, `20`, där `5` är förvalt.

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
- `AddChildAccounts` lägger till profilens konto-FK, Child-kontots synliga och normaliserade användarnamn samt unika index för profilkoppling, Household-användarnamn och Adult-e-post. Migrationen är applicerad i `syssloappen_dev`.
- `AddChildPairingCodes` skapar tabellen `ChildPairingCodes` med hash, Child-, Household- och Adult-koppling, skapad tid, utgångstid och användningstid. Migrationen är applicerad i `syssloappen_dev`.
- `AddChildDeviceSessions` skapar `ChildDeviceSessions` med Child-, konto- och Household-koppling, hashad sessionshemlighet, aktivitetstid, förnybar utgångstid, absolut maxgräns och återkallelsetid. Migrationen är applicerad i `syssloappen_dev`.
- `AddHouseholdFamilyCodes` lägger till unik familjekodshash, sista fyra tecken och rotationstid på `Households`. Den säkra backfillen och det unika indexet är applicerade i `syssloappen_dev`.
- `AddChores` skapar tabellen `Chores` med Household-, skaparkonto-, titel-, beskrivnings- och tidsfält samt främmande nycklar och index. Migrationen är applicerad i `syssloappen_dev`.
- `AddChoreAssignments` skapar tabellen `ChoreAssignments` med Household-, sysslo-, barn-, tilldelande Adult- och tidsfält samt främmande nycklar och index. Migrationen är applicerad i `syssloappen_dev`.
- `AddChoreAssignmentSubmission` lägger till textstatus med standardvärdet `Assigned` och nullable `SubmittedAt` på `ChoreAssignments`. Migrationen är applicerad i `syssloappen_dev`.
- `AddAdultReviewAndChorePoints` lägger till poäng på sysslor och tilldelningar, granskningsfält på tilldelningar samt `ChoreCompletions` med unikt Assignment-ID och utdelade poäng. Migrationen är applicerad i `syssloappen_dev`.

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

Fyra integrationstester för US-030 verifierar Adult-behörighet, validering, backendstyrt Household och skaparkonto trots manipulerade fält, synlighet för Adults i samma Household samt isolering från andra Households. Alla 58 integrationstester är godkända i Release-konfiguration.

Elva testfall för US-031:s avgränsade backenddel verifierar Adult-behörighet, korrekt och beständig tilldelning, backendstyrt Household, tilldelande Adult och tid, aktiva barn, samma-Household-krav, Household-isolering, manipulerade, oväntade, ogiltiga och obefintliga ID-/ägarfält samt regression för Adult-login, barnhantering, enhetskoppling och skapande/listning av sysslor. Alla 69 integrationstester är godkända i Release-konfiguration.

Åtta integrationstester för den avgränsade Child-vyn verifierar Child-behörighet, svarsdata och sortering, tom privat vy, syskonisolering, Household-isolering, ignorerade manipulerade query-parametrar, skydd mot inkonsekventa databasrader, omedelbar nekning efter avaktivering, bevarad historisk tilldelning samt åtkomst efter både enhetskoppling och reservinloggning. Alla 77 integrationstester är godkända i Release-konfiguration.

Elva testfall för US-041:s rapporteringsdel verifierar Child-behörighet, korrekt och beständig statusövergång, backendstyrt Child, Household, ägare, status och rapporteringstid, syskon- och Household-isolering, inkonsekventa databasrader, avaktiverat barn, manipulerade och oväntade ID-/ägarfält, ogiltigt och obefintligt ID, upprepad rapportering, status i barnets listning samt regression för Adult-login, barnhantering, Child-session, sysslor och tilldelning. Hela sviten med 88 integrationstester är godkänd i Release-konfiguration.

Ett idempotent PostgreSQL-script från `AddChoreAssignments` till `AddChoreAssignmentSubmission` har genererats och granskats. Migrationen applicerades därefter via EF Core i `syssloappen_dev`. Ett manuellt PostgreSQL-smoke-test verifierade Adult-registrering och login, två Households, tre barn, tre sysslor och tre tilldelningar, Child-enhetskoppling, privat listning med `Assigned`, backendstyrd övergång till `PendingApproval`, beständig `SubmittedAt`, HTTP 404 för syskon och annat Household samt HTTP 409 vid upprepad rapportering. Testuppgifterna var slumpmässiga, lösenordet fanns endast i minnet och smoke-märkt data lämnades kvar enligt tidigare praxis.

Sju nya integrationstester för Adult-granskning och poäng verifierar standardvärde och de fyra tillåtna poängvärdena, nekade ogiltiga värden, snapshot vid tilldelning, manipulerade ägar-/poäng-/status-/tidsfält, Adult-behörighet, Household-isolering, granskningslistan, approval, rejection, kommentar, omrapportering, completion, exakt en utdelning och privata Child-poängsummor. Hela sviten med 95 integrationstester är godkänd i Release-konfiguration.

Ett idempotent PostgreSQL-script från `AddChoreAssignmentSubmission` till `AddAdultReviewAndChorePoints` har genererats och granskats utan att köras. Det använder en transaktion, ger befintliga sysslor och tilldelningar 5 poäng, lägger till granskningsfält och check constraints, skapar `ChoreCompletions` med främmande nycklar och ett unikt Assignment-index samt skriver migrationshistorikraden. Migrationen applicerades därefter via EF Core i `syssloappen_dev`.

Ett manuellt PostgreSQL-smoke-test av Adult-granskning och poäng verifierade standardvärdet 5, HTTP 400 för värdet 6, snapshotvärdena 10 och 20, Adult-listning av `PendingApproval`, HTTP 404 för granskning från annat Household, `NeedsRedo` med kommentar och noll poäng, omrapportering, backendstyrda godkännanden med 10 och 20 poäng, totalsumman 30 samt HTTP 409 vid upprepat godkännande. Flera avbrutna smoke-försök skapade ytterligare tydligt smoke-märkta Households innan ett PowerShell 5-specifikt listproblem i testskriptet rättades; inga klartextlösenord sparades.

Alla tolv migrationer till och med `AddAdultReviewAndChorePoints` är applicerade i `syssloappen_dev`. Ett manuellt end-to-end-smoke-test mot PostgreSQL verifierade Adult-registrering och login, barnskapande, maskerad familjekodsstatus, rotation och omedelbar ogiltigförklaring av den gamla koden, enhetskoppling, beständig HttpOnly-cookie, Child-logout, skiftlägesokänslig reservlogin, skydd mot manipulerade ID-/rollfält, Adult-listning och återkallelse av session samt omedelbar nekning efter avaktivering. Den glidande förnyelsealgoritmen är verifierad av integrationstesterna; smoke-testet verifierade löpande sessionvalidering mot PostgreSQL utan att manipulera tidsstämplar manuellt.

Release-build och formatteringskontroll är godkända utan fel eller varningar. EF Core rapporterar inga väntande modelländringar utanför migrationen. Ett idempotent PostgreSQL-script från `AddChildPairingCodes` till `AddChildDeviceSessions` har genererats och granskats: det skapar endast den nya sessionstabellen, främmande nycklar, index och migrationshistorikraden i en transaktion. Migrationen har applicerats via EF Core; det separat genererade scriptet kördes inte.

Ett idempotent PostgreSQL-script från `AddChildDeviceSessions` till `AddHouseholdFamilyCodes` har också genererats och granskats. Det lägger till de tre familjekodskolumnerna, backfyller befintliga Households utan klartexthemlighet, gör hashkolumnen obligatorisk, skapar det unika indexet och skriver migrationshistorikraden i en transaktion. Migrationen har applicerats via EF Core; det separat genererade scriptet kördes inte.

Ett idempotent PostgreSQL-script från `AddHouseholdFamilyCodes` till `AddChoreAssignments` har genererats och granskats. Det innehåller först `AddChores` och därefter `AddChoreAssignments`; varje migration skapar endast sina tabeller, främmande nycklar, index och migrationshistorikrad i en egen transaktion. Båda migrationerna har applicerats via EF Core och en efterkontroll visar inga väntande migrationer. Det separat genererade scriptet kördes inte.

Ett manuellt US-030/US-031-smoke-test mot PostgreSQL verifierade Adult-registrering och login, barnskapande, enhetskoppling och Child-session, syssleskapande och listning samt beständig tilldelning. Testet verifierade också HTTP 401 utan login, HTTP 403 för Child, HTTP 404 för sysslor och barn från ett annat Household, nekad tilldelning till ett avaktiverat barn samt att manipulerade ID-, Household-, ägar-, roll- och tidsfält inte kunde styra den sparade tilldelningen. Smoke-testet skapade två isolerade test-Households med slumpmässiga uppgifter; lösenorden fanns endast i minnet och dokumenterades inte.

Ett manuellt smoke-test av Child-vyn mot PostgreSQL verifierade två egna tilldelningar med rätt svarsdata och nyaste-först-sortering, ignorerade manipulerade `ChildId`-/`HouseholdId`-parametrar, syskonisolering och isolering mellan två Households. Samma privata vy fungerade efter både Adult-styrd enhetskoppling och reservinloggning. Anonyma användare fick HTTP 401, Adults HTTP 403 och ett avaktiverat barns redan utgivna session nekades omedelbart medan den historiska tilldelningen låg kvar. Ingen migration behövdes eller applicerades.

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

Den avgränsade US-021-delen med reservinloggning via familjekod, barnvänligt användarnamn och Identity-lösenord är färdig, testad och mergad. Alla tillhörande migrationer är applicerade och de centrala flödena är smoke-testade mot PostgreSQL.

US-030 med Adult-skapade, Household-isolerade sysslor är färdig och testad automatiskt samt mot PostgreSQL. `AddChores` är applicerad i `syssloappen_dev`.

US-031:s avgränsade backenddel, där en Adult tilldelar en syssla till ett aktivt barn i samma Household, är färdig och testad automatiskt samt mot PostgreSQL. `AddChores`, `AddChoreAssignments` och alla senare migrationer är applicerade; inga migrationer väntar.

Barnets autentiserade, Household-isolerade läsning och rapportering samt Adult-listning, approval/rejection, completion och det beslutade poängsystemets backend är färdiga, mergade, migrerade och smoke-testade mot PostgreSQL. Frontend, inklusive titelruta och poängrullista, återstår.

## Kända kvarvarande saker

- Standardendpointet `WeatherForecast` från projektmallen finns fortfarande kvar och kan tas bort i en separat liten städändring.
- Ingen frontend finns ännu.
- Ingen e-postbekräftelse eller lösenordsåterställning ingår i MVP-arbetet ännu.
- ChildProfiles som skapades i utvecklingsdatabasen före enstegsflödet fick inte automatiskt användarnamn och lösenord när migrationen applicerades; de behöver hanteras eller återskapas innan de kan använda Child-login.
- PostgreSQL-smoke-körningarna, inklusive transportfelsökningen inför den godkända Child-vy-körningen, skapade flera isolerade test-Households i `syssloappen_dev`. Alla namn och konton är smoke-märkta; testlösenorden genererades endast i minnet och är inte dokumenterade.
- Household-isolering är automatiskt testad för barn, sysslor, Adult-tilldelning/listning/granskning, Child-listning/rapportering och Child-poäng. Adult-/poängflödet är också smoke-testat mot PostgreSQL; frontend återstår.
