# Syssloappen – User Stories och Acceptance Criteria

## 1. Projektöversikt

Syssloappen är en familjeorienterad applikation där vuxna kan skapa och tilldela sysslor till barn, och där barn kan logga in på sina egna enheter för att se och markera sina sysslor som färdiga.

Varje familj ska fungera som en separat enhet, ett **Household**. Användare som tillhör ett Household får aldrig kunna läsa eller ändra information som tillhör ett annat Household.

### Planerad teknikstack

- **Frontend:** Angular
- **CSS/UI:** Tailwind CSS
- **Backend:** C# / ASP.NET Core Web API
- **Databas:** PostgreSQL eller SQL Server
- **Authentication:** ASP.NET Core Authentication
- **Framtida möjlighet:** Progressive Web App (PWA)

---

# 2. Grundläggande begrepp

## Household

Ett Household representerar en familj.

Exempel:

```text
Household A
├── Adult
├── Adult
├── Child
└── Child

Household B
├── Adult
└── Child
```

All relevant data ska vara kopplad till ett Household.

Det gäller exempelvis:

- användare
- barn
- sysslor
- tilldelningar
- utförda sysslor

## Roller

Systemet ska initialt ha två roller:

### Adult

En vuxen användare som administrerar familjen och dess sysslor.

### Child

En barnanvändare som kan se och utföra sina tilldelade sysslor.

---

# 3. Övergripande säkerhetsregel

En användare får endast komma åt information som tillhör användarens eget Household.

Detta måste kontrolleras i backend.

Det räcker **inte** att endast dölja information i frontend.

Exempel:

```text
User HouseholdId = 10
Child HouseholdId = 10
→ Access allowed

User HouseholdId = 10
Child HouseholdId = 15
→ Access denied
```

---

# 4. User Stories – Authentication

## US-001 – Vuxen kan skapa konto

**Som vuxen**
vill jag kunna skapa ett konto
så att jag kan börja använda systemet.

### Acceptance Criteria

- [ ] En vuxen ska kunna registrera ett konto.
- [ ] Kontot ska kunna autentiseras på ett säkert sätt.
- [ ] Lösenord får aldrig lagras i klartext.
- [ ] En ny vuxen utan tidigare familjekoppling ska kunna skapa ett Household.
- [ ] Den vuxna ska automatiskt kopplas till sitt nya Household.
- [ ] Den vuxna ska få rollen `Adult`.

---

## US-002 – Användare kan logga in

**Som användare**
vill jag kunna logga in
så att systemet kan identifiera mig och visa rätt information.

### Acceptance Criteria

- [ ] En användare ska kunna logga in med sina inloggningsuppgifter.
- [ ] Felaktiga inloggningsuppgifter ska inte ge åtkomst.
- [ ] Backend ska kunna identifiera den autentiserade användaren.
- [ ] Backend ska kunna identifiera användarens roll.
- [ ] Backend ska kunna identifiera vilket Household användaren tillhör.

---

## US-003 – Användare kan logga ut

**Som användare**
vill jag kunna logga ut
så att någon annan inte får tillgång till mitt konto.

### Acceptance Criteria

- [ ] Det ska finnas en logout-funktion.
- [ ] Efter logout ska skyddade delar av systemet inte längre vara tillgängliga.
- [ ] Användaren ska behöva autentisera sig igen för att återfå åtkomst.

---

# 5. User Stories – Household

## US-010 – Vuxen kan skapa ett Household

**Som vuxen**
vill jag kunna skapa en familj
så att mina familjemedlemmar och vår data hålls separerade från andra familjer.

### Acceptance Criteria

- [ ] Ett Household ska ha ett unikt ID.
- [ ] Personen som skapar Household ska bli medlem i det.
- [ ] Skaparen ska ha rollen `Adult`.
- [ ] Data från andra Households får inte vara tillgänglig.

---

## US-011 – Vuxen kan bjuda in en annan vuxen

**Som vuxen**
vill jag kunna bjuda in en annan vuxen till min familj
så att vi båda kan administrera barn och sysslor.

### Acceptance Criteria

- [ ] Endast en Adult får bjuda in en annan Adult.
- [ ] Den nya vuxna ska efter accepterad inbjudan tillhöra samma Household.
- [ ] Den nya vuxna ska kunna administrera barn i detta Household.
- [ ] Den nya vuxna ska kunna skapa och tilldela sysslor.
- [ ] En användare från ett annat Household ska inte automatiskt få åtkomst.
- [ ] En inbjudan ska inte kunna ge tillgång till ett annat Household än det som skapade inbjudan.

---

## US-012 – Household-data är isolerad

**Som användare**
vill jag att min familjs information ska vara isolerad från andra familjer
så att andra användare inte kan se eller ändra vår information.

### Acceptance Criteria

- [ ] En Adult får endast se barn i sitt eget Household.
- [ ] En Adult får endast administrera barn i sitt eget Household.
- [ ] En Adult får endast skapa sysslor för sitt eget Household.
- [ ] En Adult får endast tilldela sysslor till barn i sitt eget Household.
- [ ] Ett Child får endast se information som tillhör sitt eget Household.
- [ ] Backend ska kontrollera Household-tillhörighet.
- [ ] Manipulering av ID:n i API-anrop får inte ge åtkomst till ett annat Households data.

---

# 6. User Stories – Barnkonton

## US-020 – Vuxen kan skapa barn

**Som vuxen**
vill jag kunna lägga till ett barn i min familj
så att jag kan tilldela sysslor till barnet.

### Acceptance Criteria

- [ ] Endast en Adult får skapa ett barn.
- [ ] Barnet ska automatiskt kopplas till den vuxnas Household.
- [ ] Barnet ska ha ett eget unikt ID.
- [ ] Barnet ska inte kunna kopplas till ett annat Household genom ett modifierat API-anrop.
- [ ] Alla Adults i samma Household ska kunna se barnet.

---

## US-021 – Vuxen kan skapa barnets inloggning

**Som vuxen**
vill jag kunna skapa inloggningsuppgifter åt mitt barn
så att barnet kan logga in på sin egen enhet.

### Acceptance Criteria

- [ ] En Adult ska kunna skapa ett användarkonto åt ett barn.
- [ ] Barnkontot ska kopplas till rätt Child.
- [ ] Barnkontot ska tillhöra samma Household som barnet.
- [ ] Barnkontot ska få rollen `Child`.
- [ ] Barnet ska kunna logga in med sina egna uppgifter.
- [ ] En Adult får inte skapa login för ett barn i ett annat Household.

---

## US-022 – Barn ser endast sin egen information

**Som barn**
vill jag bara se information som gäller mig
så att appen blir enkel och privat.

### Acceptance Criteria

- [ ] Barnet ska endast se sina egna tilldelade sysslor.
- [ ] Barnet ska inte kunna se syskonens privata vy.
- [ ] Barnet ska inte kunna se barn från andra Households.
- [ ] Barnet ska inte kunna ändra vilket ChildId som används för att få tillgång till någon annans data.
- [ ] Backend ska identifiera barnet från den autentiserade användaren.

---

# 7. User Stories – Sysslor

## US-030 – Vuxen kan skapa en syssla

**Som vuxen**
vill jag kunna skapa en syssla
så att den senare kan tilldelas ett barn.

### Acceptance Criteria

- [ ] Endast en Adult får skapa sysslor.
- [ ] Sysslan ska ha ett namn.
- [ ] Sysslan ska kopplas till den vuxnas Household.
- [ ] En syssla från ett Household får inte vara synlig i ett annat Household.
- [ ] Systemet ska spara vem som skapade sysslan.

### Exempel

```text
Mata katten
Städa rummet
Töm diskmaskinen
Ta ut soporna
```

---

## US-031 – Vuxen kan tilldela en syssla

**Som vuxen**
vill jag kunna tilldela en syssla till ett barn
så att barnet ser vad det ska göra.

### Acceptance Criteria

- [ ] Endast en Adult får tilldela sysslor.
- [ ] Sysslan och barnet måste tillhöra samma Household.
- [ ] Den vuxna måste tillhöra samma Household som barnet.
- [ ] Tilldelningen ska sparas i databasen.
- [ ] Barnet ska kunna se tilldelningen efter inloggning.
- [ ] Det ska inte gå att tilldela en syssla till ett barn från ett annat Household.

---

## US-032 – Vuxen kan se familjens tilldelade sysslor

**Som vuxen**
vill jag kunna se familjens aktuella sysslor
så att jag vet vem som ska göra vad.

### Acceptance Criteria

- [ ] Adults ska kunna se Householdets barn.
- [ ] Adults ska kunna se vilka sysslor varje barn har.
- [ ] Information från andra Households får inte visas.

---

# 8. User Stories – Barnets vy

## US-040 – Barn kan se sina sysslor

**Som barn**
vill jag kunna se mina aktuella sysslor
så att jag vet vad jag ska göra.

### Acceptance Criteria

- [ ] Barnet måste vara inloggat.
- [ ] Barnet ska endast se sysslor som är tilldelade det aktuella barnet.
- [ ] Barnet ska kunna se om en syssla är klar eller inte.
- [ ] Sysslor som tillhör syskon eller andra familjer får inte visas.

### Exempel

```text
Mina sysslor

[ ] Mata katten
[ ] Städa rummet
[x] Ta ut soporna
```

---

## US-041 – Barn kan markera en syssla som klar

**Som barn**
vill jag kunna markera en syssla som färdig
så att mina föräldrar kan se att jag har gjort den.

### Acceptance Criteria

- [ ] Barnet måste vara inloggat.
- [ ] Barnet får endast markera sina egna tilldelningar som färdiga.
- [ ] Ett barn får inte markera ett syskons syssla som färdig.
- [ ] En completion ska sparas i databasen.
- [ ] Systemet ska spara vilket barn som utförde sysslan.
- [ ] Systemet ska spara vilken syssla som utfördes.
- [ ] Systemet ska spara tidpunkten då sysslan markerades som klar.

---

# 9. User Stories – Vuxenvy

## US-050 – Vuxen kan se utförda sysslor

**Som vuxen**
vill jag kunna se vilka sysslor barnen har gjort
så att jag kan följa deras aktivitet.

### Acceptance Criteria

- [ ] Adults ska kunna se utförda sysslor inom sitt Household.
- [ ] Det ska framgå vilket barn som gjorde sysslan.
- [ ] Det ska framgå vilken syssla som utfördes.
- [ ] Det ska framgå när den markerades som klar.
- [ ] Information från andra Households får inte visas.

### Exempel

```text
Senaste aktiviteter

✓ Anna – Mata katten – 07:42
✓ Erik – Töm diskmaskinen – 08:15
✓ Anna – Städa rummet – 10:32
```

---

# 10. Behörighetsmatris

| Funktion                     | Adult           | Child |
| ---------------------------- | --------------- | ----- |
| Logga in                     | Ja              | Ja    |
| Logga ut                     | Ja              | Ja    |
| Se egna sysslor              | Ja\*            | Ja    |
| Se familjens barn            | Ja              | Nej   |
| Skapa barn                   | Ja              | Nej   |
| Skapa barnkonto              | Ja              | Nej   |
| Skapa syssla                 | Ja              | Nej   |
| Tilldela syssla              | Ja              | Nej   |
| Markera egen syssla klar     | Nej/ej relevant | Ja    |
| Se familjens completions     | Ja              | Nej   |
| Bjuda in Adult               | Ja              | Nej   |
| Administrera annat Household | Nej             | Nej   |

- Adult-vyn behöver inte nödvändigtvis använda samma typ av tilldelning som Child-vyn.

---

# 11. Föreslagen datamodell

Detta är en initial modell och kan ändras under implementationen.

## Household

```text
Id
Name
CreatedAt
```

## User

```text
Id
HouseholdId
Username / Email
PasswordHash
Role
CreatedAt
```

## ChildProfile

```text
Id
HouseholdId
UserId
Name
```

`UserId` kan eventuellt vara null innan ett login har skapats åt barnet.

## Chore

```text
Id
HouseholdId
CreatedByUserId
Title
Description
CreatedAt
```

## ChoreAssignment

```text
Id
HouseholdId
ChoreId
ChildId
AssignedByUserId
AssignedAt
```

## ChoreCompletion

```text
Id
HouseholdId
AssignmentId
ChildId
CompletedAt
```

---

# 12. Viktiga backend-regler

Backend ska betraktas som den auktoritativa delen av systemet.

Frontend får aldrig vara den enda säkerhetskontrollen.

Exempel:

```text
BAD:

Frontend gömmer knappen för andra barn.
Backend accepterar ändå vilket ChildId som helst.
```

```text
GOOD:

Frontend visar endast korrekt data.

OCH

Backend verifierar:
- användarens identitet
- användarens roll
- användarens HouseholdId
- resursens HouseholdId
- att användaren har rätt att utföra operationen
```

---

# 13. MVP

Första fungerande versionen ska vara liten.

## Ska ingå

- [ ] Adult kan skapa konto.
- [ ] Adult kan skapa ett Household.
- [ ] Adult kan skapa barn.
- [ ] Adult kan skapa login åt barn.
- [ ] Adult kan skapa sysslor.
- [ ] Adult kan tilldela sysslor.
- [ ] Child kan logga in.
- [ ] Child kan se sina egna sysslor.
- [ ] Child kan markera en syssla som klar.
- [ ] Adult kan se utförda sysslor.
- [ ] Household-isolering fungerar.
- [ ] Authentication och authorization fungerar.

---

# 14. Inte MVP

Följande funktioner kan vara intressanta senare men ska **inte byggas innan kärnfunktionerna fungerar**.

- Poängsystem
- Belöningar
- Veckopeng
- Badges/achievements
- Pushnotiser
- E-postnotiser
- Statistik
- Diagram
- Streaks
- Kalender
- Återkommande avancerade scheman
- Anpassningsbara avatarer
- Tema/personliga färger
- Offline-läge
- Native Android/iOS-app
- Gamification

---

# 15. Möjliga framtida funktioner

## Återkommande sysslor

Exempel:

```text
Mata katten
Varje dag

Ta ut soporna
Varje tisdag

Städa rummet
Varje söndag
```

---

## Poäng och belöningar

Sysslor skulle kunna ge poäng.

```text
Mata katten       +5
Städa rummet     +10
Töm diskmaskinen  +5
```

Barnet skulle senare kunna använda poängen för familjedefinierade belöningar.

---

## PWA

Angular-applikationen kan senare göras till en Progressive Web App.

Målet är att barnet ska kunna installera appen på sin surfplatta och få en ikon ungefär som en vanlig app.

PWA-arbetet bör göras **efter att webbversionens kärnfunktionalitet fungerar**.

---

# 16. Development Guidelines för AI-assisterad utveckling

Projektet kommer att utvecklas med hjälp av AI-verktyg som Codex och GitHub Copilot.

Följande regler ska användas vid AI-genererade ändringar.

## Ändra små delar åt gången

AI:n ska inte försöka implementera hela projektet i ett enda steg.

Arbeta istället med en user story åt gången.

Exempel:

```text
Implement US-040.

Do not implement future user stories unless they are strictly
required as dependencies for US-040.
```

---

## Förklara ändringar

Efter en implementation ska AI:n kort förklara:

1. Vilka filer som skapades eller ändrades.
2. Vad koden gör.
3. Varför lösningen valdes.

---

## Förklara nya koncept

Om implementationen introducerar ett nytt C#, ASP.NET Core eller Angular-koncept ska AI:n kort förklara konceptet.

Exempel:

```text
DbContext
Dependency Injection
DTO
Entity Framework
Middleware
Authentication
Authorization
Angular Service
Observable
Route Guard
Interceptor
```

---

## Kommentera kod där det hjälper förståelsen

Kod ska kommenteras där syftet inte är uppenbart.

Bra exempel:

```csharp
// Read the authenticated user's HouseholdId.
// This value is used to ensure that users cannot access
// resources belonging to another household.
```

Undvik kommentarer som bara upprepar koden.

Dåligt exempel:

```csharp
// Return chores
return chores;
```

---

## Ingen onödig arkitektur

AI:n ska välja den enklaste rimliga lösningen som uppfyller nuvarande krav.

Undvik att introducera:

- microservices
- message queues
- CQRS
- event sourcing
- komplexa design patterns
- extra abstractions

om det inte finns ett konkret behov.

---

# 17. Definition of Done

En user story betraktas som färdig när:

- [ ] Acceptance Criteria är uppfyllda.
- [ ] Backend validerar behörighet där det behövs.
- [ ] Household-isolering har beaktats.
- [ ] Felhantering finns för relevanta fall.
- [ ] Funktionen har testats.
- [ ] AI-genererad kod har förklarats.
- [ ] Viktig eller icke-uppenbar kod har kommentarer.
- [ ] Ingen funktion utanför storyns scope har implementerats utan anledning.

---

# 18. Prioriterad implementation

Föreslagen ordning:

```text
1. Projektstruktur
   ↓
2. Databas + grundläggande modeller
   ↓
3. Household
   ↓
4. Adult authentication
   ↓
5. Skapa Child
   ↓
6. Child authentication
   ↓
7. Skapa Chore
   ↓
8. Tilldela Chore
   ↓
9. Child: "Mina sysslor"
   ↓
10. Markera Chore som klar
   ↓
11. Adult: se completions
   ↓
12. Lägg till ytterligare Adult
   ↓
13. UI-förbättringar
   ↓
14. PWA
```

---

# 19. Projektets kärnprincip

Den viktigaste regeln för systemets arkitektur är:

> **En användares identitet och Household ska bestämma vilken data användaren får komma åt. Klienten ska aldrig själv kunna välja sig till åtkomst till en annan användares eller familjs data.**

Systemet ska därför designas utifrån:

```text
Authenticated User
        ↓
      Role
        ↓
   HouseholdId
        ↓
Authorization
        ↓
Allowed data
```
