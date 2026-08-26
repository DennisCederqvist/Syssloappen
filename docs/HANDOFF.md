# Syssloappen - Project Handoff

Senast uppdaterad: 2026-08-26

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
- Integrationstestprojektet använder en tillfällig SQLite-databas. Hela sviten omfattar nu 118 godkända backendtester för autentisering, Household-isolering, barn, Child-sessioner, sysslor, tilldelningar, cancellation, rapportering, Adult-granskning, poäng och belöningar.

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
- Historiska tilldelningar bevaras vid avaktivering. Det breda kriteriet om historiska tilldelningar, godkännanden och completions är fortsatt okryssat i `REQUIREMENTS.md` tills hela formuleringen har verifierats uttryckligen.

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
- På mergad `main` markerar lyckad inlösen koden använd och skapar en beständig, återkallningsbar `ChildDeviceSession` med HttpOnly-cookie. Samma kod kan inte användas igen.
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

US-033 är implementerad, användartestad och mergad till `main`:

- `Chore.IsActive` har lagts till med standardvärdet `true` genom migrationen `AddChoreSoftDelete`.
- `GET /api/chores` returnerar endast aktiva mallar i den autentiserade Adult-användarens Household.
- `PUT /api/chores/{choreId}` ändrar titel, valfri beskrivning och `5`/`10`/`15`/`20` poäng. ID kombineras med backendhärlett Household och requestens oväntade ägar-, Household- och aktivitetsfält kan inte styra raden.
- `DELETE /api/chores/{choreId}` sätter `IsActive = false`; varken sysslan, tilldelningar eller completions raderas.
- `POST /api/chore-assignments` kräver nu att den valda sysslan är aktiv. Befintliga tilldelningar fortsätter att visas och behåller sina snapshot-poäng efter redigering eller avaktivering.
- Angular-sidan kan redigera titel, beskrivning och poäng. Varje sysslekort har ett litet kryss med ett dynamiskt tillgängligt namn, och en inline-bekräftelse krävs innan avaktivering.
- Lyckad ändring ersätter kortet direkt i den sorterade lokala listan. Lyckad avaktivering tar direkt bort kortet och stänger eventuell redigerings- eller tilldelningspanel som använde mallen.
- Fem nya integrationstester verifierar Adult-roll, Household-isolering, manipulerade och ogiltiga ID:n/fält, validering, redigering, gamla och nya poängsnapshots, aktiv filtrering, blockerad nytilldelning samt bevarad assignment-, completion- och poänghistorik. Hela backendsviten omfattar nu 100 godkända tester i Release.

US-034 är implementerad, användartestad och mergad till `main`:

- `DELETE /api/chore-assignments/{assignmentId}` låter endast en autentiserad Adult avbryta en tilldelning i sitt eget Household.
- Tilldelningar i `Assigned`, `PendingApproval` och `NeedsRedo` får status `Cancelled`; databasraden, rapporteringstid och eventuell tidigare granskningsinformation bevaras.
- `CancelledByUserId` och `CancelledAt` sätts alltid från den autentiserade Adult-användaren och backendens `TimeProvider`. Requestens oväntade Child-, Household-, status-, Adult- och tidsfält kan inte styra auditinformationen.
- `Approved` kan inte avbrytas, så completion och utdelade poäng förblir orörda. Statusens befintliga concurrency token skyddar även samtidiga cancel-/review-anrop.
- Standardanropet `GET /api/chore-assignments` visar aktuella tilldelningar. En Adult kan hämta bevarad historik inklusive `Cancelled` genom `?includeCancelled=true`.
- Child-listningen filtrerar bort `Cancelled`, och ett barn får neutralt HTTP 404 om det försöker rapportera en avbruten tilldelning.
- Adult-frontendens aktuella lista har en tillgängligt namngiven `Ta bort`-knapp, inline-bekräftelse och direkt lokal borttagning efter HTTP 204.
- Sex nya integrationstester verifierar roll, Household-isolering, manipulerade fält, backendstyrd audit, alla tillåtna statuslägen, skydd av `Approved`, bevarad historik/poäng, Child-filtrering och säker hantering av ogiltiga, saknade och upprepade anrop. Hela backendsviten omfattar nu 106 godkända tester i Release.

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
- Svaret innehåller tilldelnings-ID, sysslo-ID, titel, valfri beskrivning, tilldelningstid, snapshot-poäng, status, rapporteringstid och eventuell omarbetningskommentar. Nyaste tilldelningar visas först.
- Både Adult-styrd enhetskoppling och reservinloggning ger åtkomst till samma privata vy. Avaktivering gör sessionen ogiltig medan den historiska tilldelningsraden bevaras.
- Själva US-040-delen ändrade ingen datamodell. Status, rapportering, Adult-granskning, completions och poäng har därefter lagts till i de efterföljande backenddelarna. Den riktiga Child-vyn för dessa flöden återstår i frontend.

US-041:s avgränsade rapporteringsdel är implementerad, testad och mergad till `main`:

- `POST /api/child/chore-assignments/{assignmentId}/submit` kräver en autentiserad användare med rollen `Child`; anonyma användare får HTTP 401 och Adults HTTP 403.
- Endpointen har ingen request-DTO. Klienten väljer därför inte Child, Household, ägare, status eller tid. Backend härleder konto och Household från den validerade Identity-cookien, hittar den aktiva barnprofilen och använder serverns `TimeProvider`.
- Tilldelningen hämtas med tilldelnings-ID, autentiserat barn, konto, aktiv status och samma Household på tilldelning, barn och syssla i samma SQL-fråga. Syskons, andra Households, obefintliga och inkonsekventa rader ger neutralt HTTP 404.
- Ett positivt ID krävs. Noll och negativa ID:n ger HTTP 400. Oväntade JSON-fält ignoreras eftersom endpointen inte binder någon request body och kan inte styra den sparade raden.
- En egen tilldelning i läget `Assigned` ändras till `PendingApproval` och får `SubmittedAt` satt till aktuell UTC-tid i backend. Barn- och sysslekopplingarna bevaras på tilldelningen.
- Upprepad rapportering ger tydligt HTTP 409 och ändrar inte den första rapporteringstiden. `Status` är ett EF Core-concurrency token så två samtidiga uppdateringar inte båda kan lyckas.
- Child-listningen visar nu `Status`, nullable `SubmittedAt`, poäng och eventuell omarbetningskommentar för samtliga statuslägen: `Assigned`, `PendingApproval`, `NeedsRedo` och `Approved`.
- Ingen completion eller poäng skapas när barnet enbart rapporterar uppgiften. Completion och poäng skapas först av det efterföljande Adult-godkännandeflödet. Frontend för uppgifter och QR-presentation återstår.

Adult-granskning och poäng är implementerade, testade och mergade till `main`:

- `POST /api/chores` tar nu valfritt `Points`. Backend använder `5` som standard och accepterar endast `5`, `10`, `15` eller `20`; samma begränsning finns som check constraint i databasen.
- `ChoreAssignment.Points` är en snapshot av sysslans poäng när tilldelningen skapas. Manipulerade poäng-, status- och ägarfält i tilldelningsrequesten kan inte ändra värdet.
- `GET /api/chore-assignments` låter en Adult se Householdets tilldelningar med barn, syssla, poäng, status, rapporteringstid, granskare, granskningstid och kommentar. SQL-frågan kräver samma Household på tilldelning, barn och syssla.
- `POST /api/chore-assignments/{assignmentId}/approve` och `/reject` accepterar endast en valfri kommentar. Backend bestämmer Adult, Household, status, tid och utdelade poäng.
- Endast `PendingApproval` kan granskas. Godkännande sätter `Approved` och skapar atomärt exakt en `ChoreCompletion`; nekande sätter `NeedsRedo` utan completion eller poäng.
- Ett barn kan rapportera en `NeedsRedo`-tilldelning igen. Den gamla granskningsinformationen rensas då och status återgår till `PendingApproval` med en ny backendtid.
- Varje completion sparar Household, tilldelning, barn, syssla, godkännande Adult, UTC-tid och de utdelade snapshot-poängen. Ett unikt index på `AssignmentId` skyddar dessutom mot dubbel utdelning.
- `GET /api/child/points` summerar endast det autentiserade aktiva barnets egna, Household-konsistenta completions. Child-listningen visar tilldelningens poäng, alla fyra statuslägen och eventuell omarbetningskommentar.
- En första Angular-frontend är skapad med Adult-login, Adult-registrering, Child-enhetskoppling, Child-reservlogin, sessionsåterställning, logout och rollstyrda startsidor. Skapa-syssla-flödet med titelruta och poängrullista återstår.
- `REQUIREMENTS.md` beskriver nu det senare belöningsflödet i US-070–US-072: en Adult administrerar Householdets belöningar, barnet begär att använda intjänade poäng och en Adult hanterar förfrågan. Detta är endast kravställt och är ännu inte implementerat.
- Frontenden är byggd mobile-first för mobil och surfplatta med separata Adult- och Child-skal, stora tryckytor, bottom navigation på mobil och responsiv sidnavigation. Referensbilden har använts som visuell riktning utan att kopieras exakt.
- Adult-vyn har en riktig barnsida som hämtar aktiva barn från `GET /api/children` och skapar barnprofil och Child-konto atomärt via `POST /api/children`.
- Adult-vyn skapar en åttateckens engångskod via `POST /api/children/{childId}/pairing-codes`, visar dess utgångstid och håller klartextkoden endast i sidans tillfälliga Angular-state.
- Child-login validerar och löser in den åttateckens koden via `POST /api/auth/child/pair`. Ett manuellt end-to-end-test har verifierat separat Adult- och Child-session, privat Child-endpoint samt att samma kod inte kan återanvändas.
- Adult-vyn visar barnets kopplade sessioner med status, skapad tid, senaste aktivitet och absolut slutdatum via `GET /api/children/{childId}/device-sessions`.
- Återkallning kräver en tydlig bekräftelse i UI:t och använder `DELETE /api/children/{childId}/device-sessions/{sessionId}`. En lyckad återkallning markerar enheten utloggad och backend nekar omedelbart den tidigare Child-cookien.
- Adult-vyn redigerar barnets visningsnamn via `PUT /api/children/{childId}` och skickar endast det nya namnet. Den lokala listan sorteras om efter ett lyckat svar.
- Avaktivering ligger i en separat riskzon med tvåstegsbekräftelse och använder `DELETE /api/children/{childId}`. Barnet tas bort från den aktiva listan, öppna UI-paneler stängs och backend återkallar alla Child-sessioner medan historiken behålls.

## Child-frontend för US-040 och US-041

Child-frontenden implementerades och verifierades på `feature/child-chores-and-submission`:

- Barnets route `/barn` hämtar tilldelningar från `GET /api/child/chore-assignments` och poängsaldo från `GET /api/child/points`.
- Den tidigare visuella platshållaren är ersatt av mobile-first-kort med titel, valfri beskrivning, snapshot-poäng och svenska statusnamn för `Assigned`, `PendingApproval`, `NeedsRedo` och `Approved`.
- Sidan har särskilda loading-, fel- och tomlägen. Fel vid initial hämtning kan provas igen utan omladdning.
- `NeedsRedo` visar Adult-kommentaren. Både `Assigned` och `NeedsRedo` har en stor knapp med dynamiskt tillgängligt namn för rapportering.
- `POST /api/child/chore-assignments/{assignmentId}/submit` anropas med `null` body. Frontenden skickar inga Child-, Household-, status-, poäng-, ägar- eller tidsfält.
- Pågående assignment-ID:n hålls i lokal state så upprepade klick inte skapar dubbla samtidiga anrop. Efter ett lyckat svar uppdateras kortet direkt till `PendingApproval` med backendens rapporteringstid.
- HTTP 404, HTTP 409 och övriga rapporteringsfel visas på rätt kort med begripliga svenska feltexter.
- Tio nya Angular-tester verifierar endpointkontraktet, tom request body, riktig vydata, poängsaldo, Adult-kommentar, tillåtna statusövergångar, omedelbar lokal uppdatering, dubbelklicksskydd, rapporteringsfel, hämtningsfel med retry och tomläge. Hela frontendsviten omfattar nu 45 godkända tester.
- Prettier-kontroll och Angular-produktionsbygge är godkända. Hela den oförändrade backendsviten är fortsatt grön med 106/106 tester.
- Ett riktigt smoke-test mot det lokala API:t och PostgreSQL verifierade Child-listning med titel, beskrivning och 10 snapshot-poäng, noll initiala poäng samt hela flödet `Assigned` → `PendingApproval` → `NeedsRedo` med Adult-kommentar → omrapportering till `PendingApproval` → `Approved` och exakt 10 poäng. Inga testlösenord sparades eller dokumenterades.
- Två inledande smoke-försök skapade separata, tydligt Child-UI-smoke-märkta Households innan PowerShell 5:s kända problem med icke-ASCII JSON och arrayräkning undveks i den godkända körningen.

Child-vyn användartestades 2026-08-26 med en riktig Adult-tilldelning och Child-session. Tilldelningen visades och kunde rapporteras till `PendingApproval`. En stale Angular-devserver gjorde först att den gamla platshållaren visades trots aktuell källkod; frontend och API startades om kontrollerat och den serverade lazy-chunken verifierades innehålla den nya vyn. Användaren godkände därefter merge till `main`, som genomfördes och pushades i merge-commit `7a0fec8`. Feature-branchen raderades lokalt och på GitHub.

## Adult-frontend för US-050 och US-051

Adult-granskningen implementerades och verifierades på `feature/adult-chore-review`:

- Den nya Adult-skyddade routen `/vuxen/granska` kan öppnas från bottom navigation på samtliga Adult-sidor och från snabbvalet på Adult-startsidan.
- `GET /api/chore-assignments` fyller en separat kö med `PendingApproval` och en historik med `Approved` och `NeedsRedo`. `Assigned` visas fortsatt på syssle-/tilldelningssidan och `Cancelled` filtreras av standardendpointen.
- Varje väntande kort visar barn, syssla, snapshot-poäng och rapporteringstid samt en valfri kommentar med högst 500 tecken.
- Godkännande anropar `POST /api/chore-assignments/{assignmentId}/approve`; omarbete anropar motsvarande `/reject`. Requesten innehåller endast `{ comment }` och kan inte styra Household, Child, Adult, status, poäng eller tidsfält.
- Pågående review-ID blockerar dubbla och samtidiga klick. Ett lyckat svar uppdaterar kortet direkt till `Approved` eller `NeedsRedo`, flyttar det till historiken och visar ett tydligt resultatmeddelande.
- HTTP 400, HTTP 404, HTTP 409 och övriga fel visas på rätt kort. Sidan har dessutom loading-, retry- och tomläge.
- Knapparna har stora tryckytor och dynamiska tillgängliga namn som innehåller både barnets och sysslans namn.
- Åtta nya Angular-tester verifierar servicekontrakten, vydata, separerad kö/historik, godkännande, omarbete, trimmad eller nullable kommentar, omedelbar lokal uppdatering, dubbelklicksskydd, tillgängliga namn, konflikt och tomläge. Hela frontendsviten omfattar nu 53 godkända tester.
- Prettier-kontroll och Angular-produktionsbygge är godkända. Hela den oförändrade backendsviten är fortsatt grön med 106/106 tester.
- Ett riktigt smoke-test mot API:t och PostgreSQL verifierade `PendingApproval` med rätt barn, syssla, rapporteringstid och 10 snapshot-poäng, `NeedsRedo` med synlig Child-kommentar och noll poäng, omrapportering, `Approved`, sparad granskningsinformation och exakt 10 utdelade Child-poäng. Ett tydligt Adult-review-smoke-märkt Household skapades; testlösenordet fanns endast i processminnet och sparades inte.
- Den serverade Angular-devbundlen verifierades innehålla routen och den nya Adult-review-komponenten. Svenska tecken representeras som escape-sekvenser i devbundlen men visas normalt i webbläsaren.

Adult-granskningen användartestades 2026-08-26 med en riktig rapporterad Child-tilldelning. Både `NeedsRedo` med kommentar och ett senare godkännande fungerade. Godkännandet delade ut poängen; en redan öppen Child-vy uppdateras inte i realtid utan hämtar det aktuella saldot vid sidladdning. Användaren godkände därefter merge till `main`, som genomfördes och pushades i merge-commit `df0ead7`.

## Responsivitets- och tillgänglighetsgenomgång

Kärnflödenas breda genomgång är implementerad, verifierad, användargodkänd och mergad till `main` i merge-commit `8efa7f7`:

- Login/registrering, Adult-start, barn/konton, sysslor/tilldelningar, Adult-granskning och Child-vyn ingår.
- Den befintliga Playwright-resan kör nu responsivitetskontroller vid 390, 768 och 1280 px, nekar horisontell dokument-scroll och kräver minst 44 × 44 px för synliga knappar och länkar.
- `@axe-core/playwright` är tillagd som dev dependency och verifierar WCAG 2 A/AA samt WCAG 2.1 A/AA i centrala tillstånd genom det riktiga API-/PostgreSQL-flödet.
- Browserkörningen fångade verkliga kontrastproblem. Brand-, lilac- och muted-tokens mörkades; fokusmarkeringen är nu opak och tydlig.
- Långa header-rubriker radbryts, uppgiftsbankens kryss är 48 px och Adult-tilldelningarnas svenska statustext visas även på mobil.
- Formulärfel är kopplade till fälten. Ogiltig submit, öppnade inline-paneler, bekräftelser, borttagna åtgärdsknappar och lyckade statusövergångar får avsiktlig fokusplacering.
- Växlingsknapparna på login använder `aria-pressed` i namngivna grupper i stället för ett ofullständigt tabs-mönster. Paneler och bekräftelser har namngivna regioner/grupper, och loading-/fel-/resultatlägen använder relevanta live-statusar.
- Reduced motion stänger effektivt av dekorativa animationer och övergångar; E2E-testet kör och verifierar detta läge.
- Slutverifiering: Prettier godkänd, 55/55 Angular-tester, Angular-produktionsbygge, Playwright-E2E med Chromium/API/PostgreSQL och 106/106 backendtester i Release är gröna.

Genomgången användartestades och godkändes 2026-08-26 utan upptäckta blockerande problem. Produktbeslutet är att ytterligare grafisk UI/UX-finslipning görs i projektets slutskede, såvida inte ett specifikt användbarhets- eller tillgänglighetsproblem motiverar en tidigare ändring.

`REQUIREMENTS.md`:s fyra breda kriterier i frontendavsnittet är markerade som färdiga efter mergen till `main`.

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

Frontendprojektet använder Angular 22, standalone components, reactive forms och Tailwind CSS 4. Angulars dev-server proxyar `/api` till API:t på `http://localhost:5047`. Autentiseringstjänsten använder backendens HttpOnly-cookie, och route guards väljer rätt startsida för `Adult` respektive `Child` utan att ersätta backendens behörighetskontroller.

Adult-registreringen skapar Household och Adult-konto via `POST /api/auth/register`, visar den engångsutlämnade familjekoden och leder därefter användaren till login med förifylld e-post. Development-miljön använder `SameAsRequest` för autentiseringscookien så HTTP-proxyn fungerar lokalt; övriga miljöer behåller `SecurePolicy.Always`.

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
- `AddChoreSoftDelete` lägger till `Chores.IsActive` med standardvärdet `true`. Migrationen är applicerad i `syssloappen_dev`.
- `AddChoreAssignmentCancellation` lägger till nullable `ChoreAssignments.CancelledByUserId` och `CancelledAt`, index samt en restriktiv Adult-FK. Migrationen är applicerad i `syssloappen_dev`.
- `AddRewardsCatalog` skapar `Rewards` med Household-, skaparkonto-, namn-, beskrivnings-, poängpris-, aktiv- och tidsfält, positiv-pris-constraint, index och restriktiv skapare-FK. Migrationen är applicerad i `syssloappen_dev`.

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

Starta Angular-frontenden från `frontend/`:

```bash
npm ci
npm start
```

Frontendens lokala adress är `http://localhost:4200`. Använd ett privat webbläsarfönster eller en separat enhet för Child-flödet när en Adult-session redan är aktiv i den vanliga webbläsaren.

Kör det browserbaserade kärnflödet från `frontend/` efter att API-projektets Release-version har byggts och migrationerna har applicerats i den lokala PostgreSQL-databasen:

```bash
npx playwright install chromium
npm run e2e
```

Playwright startar API:t på port 5047 och Angular på port 4200 automatiskt om de inte redan körs. Den lokala körningen återanvänder annars befintliga servrar på dessa portar.

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

Fem integrationstester för US-033 verifierar anonym och Child-nekning, Adult-behörighet, Household-isolering, manipulerade Chore-/Household-/ägar-/aktivitetsfält, ogiltiga ID:n och redigeringsvärden, trimning, poängsnapshot före och efter ändring, soft delete, aktiv listfiltrering, blockerad nytilldelning samt bevarad godkänd assignment, completion och poäng. Hela sviten med 100 integrationstester är godkänd i Release-konfiguration.

Migrationen `AddChoreSoftDelete` har verifierats med `has-pending-model-changes` och ett granskat idempotent PostgreSQL-script. Scriptet lägger endast till den obligatoriska booleska kolumnen `Chores.IsActive` med standardvärdet `true` och migrationshistorikraden i en transaktion. Migrationen applicerades därefter via EF Core i `syssloappen_dev`.

Ett riktigt US-033-smoke-test mot API:t och PostgreSQL verifierade HTTP 404 när ett Adult-konto försökte redigera eller avaktivera ett annat Households syssla, trimning av redigerad titel och beskrivning, poängändring från 10 till 20, gamla och nya assignment-snapshots på 10 respektive 20, HTTP 204 vid avaktivering, tom aktiv sysslelista, HTTP 404 vid ny tilldelning och två bevarade historiska assignments. Ett separat komplett godkännandeflöde verifierade status `Approved`, bevarad completion och 10 bevarade barnpoäng efter att mallen avaktiverats. Några inledande smoke-försök skapade tydligt US-033-märkta test-Households innan ett PowerShell 5-problem med JSON-teckenkodning och arrayräkning isolerades; inga klartextlösenord dokumenterades eller sparades i repot.

Sex integrationstester för US-034 verifierar anonym och Child-nekning, Adult-behörighet, Household-isolering, manipulerade Assignment-/Child-/Household-/status-/Adult-/tidsfält, backendstyrd audit, `Assigned`, `PendingApproval`, `NeedsRedo` och `Approved`, soft-cancellation, explicit historik, Child-filtrering, blockerad Child-submit samt bevarad completion och poäng. Hela sviten med 106 integrationstester är godkänd i Release-konfiguration.

Migrationen `AddChoreAssignmentCancellation` har verifierats med `has-pending-model-changes` och ett granskat idempotent PostgreSQL-script. Scriptet lägger endast till nullable `CancelledAt`, nullable `CancelledByUserId`, indexet, den restriktiva FK:n till `AspNetUsers` och migrationshistorikraden i en transaktion. Migrationen applicerades därefter via EF Core i `syssloappen_dev`.

Ett riktigt US-034-smoke-test mot API:t och PostgreSQL verifierade en synlig Child-tilldelning före cancellation, HTTP 404 från ett annat Household, HTTP 204 vid giltig cancellation trots manipulerade bodyfält, tom aktuell Adult-lista, `Cancelled` med sparad Adult och tid i explicit historik, tom Child-lista, HTTP 404 vid Child-submit, HTTP 409 vid upprepad cancellation samt HTTP 409 för en `Approved` tilldelning med 10 bevarade poäng. Testet skapade två isolerade US-034-märkta Households; lösenordet fanns endast i processminnet och dokumenterades inte.

Ett idempotent PostgreSQL-script från `AddChoreAssignmentSubmission` till `AddAdultReviewAndChorePoints` har genererats och granskats utan att köras. Det använder en transaktion, ger befintliga sysslor och tilldelningar 5 poäng, lägger till granskningsfält och check constraints, skapar `ChoreCompletions` med främmande nycklar och ett unikt Assignment-index samt skriver migrationshistorikraden. Migrationen applicerades därefter via EF Core i `syssloappen_dev`.

Ett manuellt PostgreSQL-smoke-test av Adult-granskning och poäng verifierade standardvärdet 5, HTTP 400 för värdet 6, snapshotvärdena 10 och 20, Adult-listning av `PendingApproval`, HTTP 404 för granskning från annat Household, `NeedsRedo` med kommentar och noll poäng, omrapportering, backendstyrda godkännanden med 10 och 20 poäng, totalsumman 30 samt HTTP 409 vid upprepat godkännande. Flera avbrutna smoke-försök skapade ytterligare tydligt smoke-märkta Households innan ett PowerShell 5-specifikt listproblem i testskriptet rättades; inga klartextlösenord sparades.

Alla tolv migrationer till och med `AddAdultReviewAndChorePoints` är applicerade i `syssloappen_dev`. Ett manuellt end-to-end-smoke-test mot PostgreSQL verifierade Adult-registrering och login, barnskapande, maskerad familjekodsstatus, rotation och omedelbar ogiltigförklaring av den gamla koden, enhetskoppling, beständig HttpOnly-cookie, Child-logout, skiftlägesokänslig reservlogin, skydd mot manipulerade ID-/rollfält, Adult-listning och återkallelse av session samt omedelbar nekning efter avaktivering. Den glidande förnyelsealgoritmen är verifierad av integrationstesterna; smoke-testet verifierade löpande sessionvalidering mot PostgreSQL utan att manipulera tidsstämplar manuellt.

Release-build och formatteringskontroll är godkända utan fel eller varningar. EF Core rapporterar inga väntande modelländringar utanför migrationen. Ett idempotent PostgreSQL-script från `AddChildPairingCodes` till `AddChildDeviceSessions` har genererats och granskats: det skapar endast den nya sessionstabellen, främmande nycklar, index och migrationshistorikraden i en transaktion. Migrationen har applicerats via EF Core; det separat genererade scriptet kördes inte.

Ett idempotent PostgreSQL-script från `AddChildDeviceSessions` till `AddHouseholdFamilyCodes` har också genererats och granskats. Det lägger till de tre familjekodskolumnerna, backfyller befintliga Households utan klartexthemlighet, gör hashkolumnen obligatorisk, skapar det unika indexet och skriver migrationshistorikraden i en transaktion. Migrationen har applicerats via EF Core; det separat genererade scriptet kördes inte.

Ett idempotent PostgreSQL-script från `AddHouseholdFamilyCodes` till `AddChoreAssignments` har genererats och granskats. Det innehåller först `AddChores` och därefter `AddChoreAssignments`; varje migration skapar endast sina tabeller, främmande nycklar, index och migrationshistorikrad i en egen transaktion. Båda migrationerna har applicerats via EF Core och en efterkontroll visar inga väntande migrationer. Det separat genererade scriptet kördes inte.

Ett manuellt US-030/US-031-smoke-test mot PostgreSQL verifierade Adult-registrering och login, barnskapande, enhetskoppling och Child-session, syssleskapande och listning samt beständig tilldelning. Testet verifierade också HTTP 401 utan login, HTTP 403 för Child, HTTP 404 för sysslor och barn från ett annat Household, nekad tilldelning till ett avaktiverat barn samt att manipulerade ID-, Household-, ägar-, roll- och tidsfält inte kunde styra den sparade tilldelningen. Smoke-testet skapade två isolerade test-Households med slumpmässiga uppgifter; lösenorden fanns endast i minnet och dokumenterades inte.

Ett manuellt smoke-test av Child-vyn mot PostgreSQL verifierade två egna tilldelningar med rätt svarsdata och nyaste-först-sortering, ignorerade manipulerade `ChildId`-/`HouseholdId`-parametrar, syskonisolering och isolering mellan två Households. Samma privata vy fungerade efter både Adult-styrd enhetskoppling och reservinloggning. Anonyma användare fick HTTP 401, Adults HTTP 403 och ett avaktiverat barns redan utgivna session nekades omedelbart medan den historiska tilldelningen låg kvar. Ingen migration behövdes eller applicerades.

Angular-frontenden har 21 godkända tester för appskal, auth-tjänst, barnservice och Adult-barnsidan. Produktionsbygget är godkänt. Manuella frontend-smoke-tester via Angular-proxyn verifierar Adult-registrering och login, barnskapande, namnändring, generering av engångskod, inlösen på en separat Child-session, sessionslistning, Adult-återkallning, avaktivering, tom aktiv barnlista och HTTP 401 för barnets tidigare session.

Migrationen `AddChildProfiles` är applicerad i `syssloappen_dev`. Ett manuellt HTTP-test mot PostgreSQL verifierade HTTP 401 utan login, lyckad skapning som Adult och isolering mellan två Households.
Ett manuellt US-023-test mot PostgreSQL verifierade lyckad namnändring i rätt Household, HTTP 404 från ett annat Household och fortsatt isolering i barnlistan.
Migrationen `AddChildProfileSoftDelete` är applicerad i `syssloappen_dev`; PostgreSQL lade till den obligatoriska `IsActive`-kolumnen med standardvärdet `true` utan fel.

## Aktuell arbetsdel

US-070:s bildfria belöningskatalog är implementerad, automatiskt verifierad, användartestad och godkänd för merge:

- `GET/POST /api/rewards` samt `PUT/DELETE /api/rewards/{id}` kräver rollen `Adult`.
- Backend härleder alltid Household och skapande konto från den autentiserade Adult-användaren. Request-DTO:erna innehåller bara namn, valfri beskrivning och positivt heltalspris; manipulerade ID-, Household-, ägar-, aktivitets- och tidsfält kan inte styra lagrad data.
- Lista, redigering och avaktivering kombinerar resurs-ID med det autentiserade Householdet i databasfrågan. Avaktivering är soft delete, så Reward-raden finns kvar för US-071/US-072:s framtida redemption-historik.
- Angular-routen `/vuxen/belöningar` har mobile-first-katalog med skapa-, redigerings- och bekräftat avaktiveringsflöde samt loading-, fel-, tom- och bekräftelselägen. Den skickar inga ägar- eller Household-fält.
- Åtta nya integrationstester täcker anonym och Child-nekning, Adult-behörighet, Household-isolering, backendstyrt ägarskap, manipulerade fält och ID:n, validering, redigering och bevarad soft-delete-historik. Hela backendsviten är grön med 118 tester. Frontendsviten är grön med 59 tester och Angular-produktionsbygget är godkänt.
- Bilduppladdning är avsiktligt inte implementerad. Bildformat, storleksgränser och lagringsstrategi ska beslutas innan den delen byggs.

Det browserbaserade end-to-end-testet av det centrala syssleflödet är implementerat, verifierat och mergat till `main` i merge-commit `1cba8f3`. Responsivitets- och tillgänglighetsutökningen är verifierad, användargodkänd och mergad till `main` i merge-commit `8efa7f7`.

- Playwright kör Chromium i mobil viewport.
- Adult och Child använder två separata browser-contexts och därmed isolerade cookies.
- Testet skapar ett unikt E2E-märkt Household och går genom registrering, barnkonto, engångskod, syssla, tilldelning, rapportering, `NeedsRedo` med kommentar, omrapportering, godkännande och uppdaterat poängsaldo.
- Körningen använder riktiga Angular-anrop via proxyn till API:t och den lokala PostgreSQL-databasen.
- `npm run e2e` passerar med kärnflöde, Axe, reduced motion samt 390/768/1280-kontroller. Även 55/55 Angular-tester, formatteringskontroll, Angular-produktionsbygge och 106/106 backendtester i Release passerar.

Adult-frontenden för US-050/US-051 är implementerad, automatiskt verifierad, manuellt användartestad och mergad till `main` i `df0ead7`. Child-frontenden för US-040/US-041 är motsvarande mergad till `main` i `7a0fec8`.

Backendens MVP-kärna är färdig, mergad, migrerad och verifierad med 106 integrationstester. Frontendens första mobile-first-del omfattar Adult-registrering och login, sessionsåterställning, logout, rollstyrd navigation, barnkonton, kopplade enheter samt den återanvändbara uppgiftsbanken med tilldelning och cancellation. Barnets kodinlösen skapar den beständiga Child-sessionen; både enhetsåterkallning och avaktivering nekar sessionen omedelbart i backend.

Adult-flödet för sysslor och tilldelningar är användartestat och mergat till `main`. Det innehåller route `/vuxen/sysslor`, den återanvändbara uppgiftsbanken, poängsnapshots, tilldelning, US-033:s redigering/avaktivering samt US-034:s bekräftade soft-cancellation av feltilldelningar. Frontenden har nu 53 godkända tester och ett godkänt produktionsbygge; backenden har 106 godkända integrationstester i Release.

Produktbeslutet är att en `Chore` är en återanvändbar mall i Householdets uppgiftsbank, inte en engångsuppgift. En Adult skapar exempelvis `Bädda sängen` en gång och kan sedan skapa flera separata `ChoreAssignment`-rader för samma eller olika barn. Efter ny skapning får UI:t gärna leda direkt till en valfri tilldelning, men mallen finns kvar för framtida användning. Varje tilldelning fryser poängvärdet som gällde vid tilldelningstillfället.

US-030:s återanvändbara mallflöde, US-033, US-034, Child-frontenden för US-040/US-041, Adult-granskningen för US-050/US-051, det browserbaserade E2E-testet samt responsivitets- och tillgänglighetsgenomgången är implementerade, verifierade, användargodkända och mergade. US-011 är implementerad, automatiskt verifierad och redo för användartest på `feature/adult-household-invitation`.

## US-011 - Adult-inbjudan

- `POST /api/household/invitations` kräver en autentiserad Adult och skapar en 24 timmar giltig, kryptografiskt slumpmässig engångskod.
- Endast kodens SHA-256-hash sparas. Household och skapande Adult hämtas från den autentiserade användaren.
- `POST /api/auth/register/invited` tar endast invitation code, e-post och lösenord. Backend sätter Household från inbjudan och rollen till `Adult`.
- Nytt konto och förbrukad inbjudan skapas atomärt i samma databastransaktion. Utgångna och använda koder nekas.
- Angular-flödet finns på `/vuxen/bjud-in` för skaparen och `/acceptera-inbjudan` för den nya vuxna.
- Fyra nya backendtester och två frontendtester täcker behörighet, Household-isolering, engångsanvändning, utgång och manipulerade klientfält.
- Hela backendsviten är grön med 110 tester. Frontendsviten är grön med 57 tester och Angular-produktionsbygget är godkänt.
- Manuell testväg: logga in som Adult, öppna `/vuxen/bjud-in`, skapa och kopiera kod, öppna `/acceptera-inbjudan` i privat fönster, registrera den andra vuxna och logga in med det nya kontot. Kontrollera därefter att båda vuxna ser samma barn och kan skapa, tilldela och granska sysslor.
- US-011 ska inte markeras som klar eller mergas till `main` före användarens manuella godkännande.

## Kända kvarvarande saker

- Standardendpointet `WeatherForecast` från projektmallen finns fortfarande kvar och kan tas bort i en separat liten städändring.
- Frontendens barnnavigation och hela barnkontohanteringen är inkopplade: skapa, lista, redigera, avaktivera, koppla enhet samt visa och återkalla sessioner. Adult-vyn för sysslor och tilldelningar, barnets riktiga startsida och Adult-granskningen är färdiga och användartestade.
- US-070:s bildfria belöningskatalog är implementerad. Poängreservation och belöningsförfrågningar enligt US-071–US-072 återstår; bilduppladdning kommer sist när format, storleksgränser och lagring har beslutats.
- Ingen e-postbekräftelse eller lösenordsåterställning ingår i MVP-arbetet ännu.
- ChildProfiles som skapades i utvecklingsdatabasen före enstegsflödet fick inte automatiskt användarnamn och lösenord när migrationen applicerades; de behöver hanteras eller återskapas innan de kan använda Child-login.
- PostgreSQL-smoke-körningarna, inklusive transportfelsökningen inför den godkända Child-vy-körningen, skapade flera isolerade test-Households i `syssloappen_dev`. Alla namn och konton är smoke-märkta; testlösenorden genererades endast i minnet och är inte dokumenterade.
- Household-isolering är automatiskt testad för barn, sysslor, Adult-tilldelning/listning/granskning, Child-listning/rapportering och Child-poäng. Hela frontendflödet från Adult-registrering till Child-poäng är nu också browsertestat mot det riktiga API:t och PostgreSQL.
