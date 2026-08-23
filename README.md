# Syssloappen

> En familjeapp för att tilldela, hantera och följa upp sysslor i hemmet.

**Syssloappen** är en webbapplikation där vuxna kan skapa och tilldela sysslor till barn, medan barnen enkelt kan se vad de ska göra och markera sina sysslor som utförda.

Projektet är byggt med fokus på enkel användning i hemmet, tydlig separation mellan familjer och en struktur som senare kan byggas ut med fler funktioner.

---

## Vad är Syssloappen?

Tanken är enkel:

* En vuxen skapar ett hushåll.
* Barn och andra vuxna tillhör hushållet.
* Vuxna kan skapa och tilldela sysslor.
* Barn kan logga in och se sina egna uppgifter.
* Barn kan markera sysslor som genomförda.
* Vuxna kan få en överblick över vad som är gjort och vad som återstår.

Applikationen är främst tänkt att användas på surfplattor, telefoner och datorer i hemmet.

Målet är att ett barn exempelvis ska kunna öppna appen på sin surfplatta och direkt mötas av:

> **Mina sysslor**

utan att behöva navigera runt i ett komplicerat administrationssystem.

---

## Varför?

Att hålla reda på vem som ska göra vad hemma blir snabbt rörigt.

Lapplistor försvinner, muntliga överenskommelser glöms bort och vanliga todo-appar är sällan byggda kring hur ett hushåll faktiskt fungerar.

Syssloappen försöker lösa det genom att göra ansvarsfördelningen tydlig:

**Vuxna administrerar. Barn utför. Alla ser rätt information.**

Projektet är samtidigt ett praktiskt fullstackprojekt med syftet att bygga erfarenhet av en modern frontend, ett separat backend-API, autentisering, relationsdatabaser och riktig fleranvändararkitektur.

---

# Grundprincipen: Household

Den viktigaste delen av systemets arkitektur är begreppet **Household**.

Ett Household representerar en familj eller ett hushåll.

```text
Household
│
├── Adult
├── Adult
├── Child
├── Child
│
├── Chores
└── Assignments
```

All relevant information tillhör ett Household.

Det innebär att:

* användare i familj A inte får se familj B,
* sysslor i familj A inte får kunna hämtas från familj B,
* barn endast ska kunna se information de har behörighet till,
* vuxna endast administrerar sitt eget hushåll.

Detta är inte bara en funktion i användargränssnittet utan en central regel som ska upprätthållas genom hela systemet.

---

# Användarroller

## Adult

En vuxenanvändare administrerar hushållet.

En vuxen ska bland annat kunna:

* skapa sysslor,
* tilldela sysslor till barn,
* se vilka sysslor som är aktuella,
* se vad som har genomförts,
* hantera barn i hushållet,
* dela hushållet med ytterligare en vuxen.

Två vuxna i samma Household ska alltså kunna se och administrera samma familjeinformation.

---

## Child

Ett barn har ett enklare gränssnitt med fokus på de egna sysslorna.

Barnet ska exempelvis kunna:

* logga in på sin egen användare,
* se sina aktuella sysslor,
* se vad som behöver göras,
* markera en syssla som utförd.

Barnet ska inte behöva hantera administration som inte är relevant för uppgiften.

---

# Exempel

Ett Household innehåller:

```text
Familjen Andersson

Adults
├── Anna
└── Erik

Children
├── Alice
└── Emil
```

Anna skapar sysslan:

```text
Töm diskmaskinen
```

och tilldelar den till Alice.

Alice öppnar Syssloappen på sin surfplatta och ser:

```text
Mina sysslor

[ ] Töm diskmaskinen
```

När hon är färdig markerar hon sysslan som genomförd.

Anna och Erik kan därefter se ändringen från sina respektive användare.

---

# Teknik

Syssloappen byggs som en modern fullstackapplikation med frontend och backend separerade från varandra.

### Frontend

* Angular
* TypeScript
* HTML
* CSS
* Tailwind CSS

Frontend ansvarar för användargränssnittet och kommunicerar med backend genom ett API.

### Backend

* C#
* ASP.NET Core
* REST API
* Entity Framework Core

Backend ansvarar bland annat för:

* affärslogik,
* autentisering och behörighet,
* användare,
* households,
* sysslor,
* tilldelningar,
* kommunikation med databasen.

### Databas

* PostgreSQL

PostgreSQL används för att lagra applikationens data.

Exempel på information som behöver lagras är:

```text
Users
Households
HouseholdMembers
Children
Chores
Assignments
Completion status
```

Den slutliga datamodellen utvecklas tillsammans med projektet.

---

# Översiktlig arkitektur

```text
┌──────────────────────────┐
│        Angular           │
│       Frontend           │
│                          │
│  Desktop / Tablet /      │
│  Mobile / PWA            │
└────────────┬─────────────┘
             │
             │ HTTP / JSON
             │
             ▼
┌──────────────────────────┐
│     ASP.NET Core API     │
│                          │
│  Authentication          │
│  Authorization           │
│  Business logic          │
│  Household isolation     │
└────────────┬─────────────┘
             │
             │ Entity Framework
             │
             ▼
┌──────────────────────────┐
│        PostgreSQL        │
│                          │
│ Users                    │
│ Households               │
│ Chores                   │
│ Assignments              │
└──────────────────────────┘
```

---

# Säkerhet och dataisolering

En central teknisk regel i projektet är:

> **En användare får aldrig kunna komma åt data från ett Household som användaren inte tillhör.**

Det gäller även om någon försöker anropa API:t direkt istället för att använda frontend.

Backend ska därför inte förlita sig på att frontend gömmer information.

Exempel:

```http
GET /api/chores/123
```

ska inte returnera sysslan enbart för att den existerar.

API:t måste även kontrollera att den inloggade användaren har behörighet att komma åt det Household som sysslan tillhör.

---

# Projektstruktur

Projektet är uppdelat så att frontend, backend och övriga projektfiler kan utvecklas separat men fortfarande ligga i samma repository.

En förenklad struktur kan exempelvis se ut så här:

```text
Syssloappen/
│
├── backend/
│
├── frontend/
│
├── docs/
│
├── REQUIREMENTS.md
├── README.md
└── .gitignore
```

Den faktiska strukturen kan förändras när projektet växer.

---

# Requirements

Mer detaljerade krav och user stories dokumenteras i:

```text
REQUIREMENTS.md
```

README-filen beskriver projektet på en övergripande nivå medan `REQUIREMENTS.md` fungerar som den mer detaljerade specifikationen för hur systemet ska bete sig.

---

# Projektstatus

🚧 **Syssloappen är under aktiv utveckling.**

Projektet befinner sig fortfarande i ett tidigt utvecklingsstadium.

Den grundläggande utvecklingsmiljön och projektstrukturen sätts upp innan de större funktionerna implementeras.

Planerad utveckling omfattar bland annat:

* backend-projekt,
* PostgreSQL-databas,
* datamodeller,
* migrations,
* API,
* autentisering,
* Household-hantering,
* användarroller,
* sysslor,
* tilldelningar,
* Angular-frontend,
* responsivt gränssnitt.

---

# Utvecklingsmål

Syssloappen är både ett faktiskt applikationsprojekt och ett lärprojekt.

Projektet används för att få praktisk erfarenhet av bland annat:

* C#,
* ASP.NET Core,
* Angular,
* TypeScript,
* PostgreSQL,
* Entity Framework Core,
* REST API-design,
* autentisering,
* authorization,
* relationsdatabaser,
* Git,
* GitHub,
* fullstackarkitektur.

Målet är därför inte bara att få applikationen att fungera.

Koden ska även vara begriplig och strukturerad så att det går att förstå **varför** en lösning fungerar.

---

# Utvecklingsprinciper

Projektet följer några grundläggande principer.

### Enkelhet

Det ska vara enkelt för ett barn att använda appen utan instruktioner.

### Tydliga roller

Barn och vuxna har olika behov och ska därför inte behöva använda exakt samma gränssnitt.

### Household isolation

Familjer ska vara fullständigt separerade från varandra.

### Backend ansvarar för säkerheten

Frontend får aldrig vara den enda spärren mot information som en användare inte ska komma åt.

### Bygg först det som behövs

Projektet börjar med kärnfunktionerna och kan därefter byggas ut.

---

# Möjlig framtida utveckling

När kärnfunktionerna fungerar finns möjlighet att bygga vidare med exempelvis:

* återkommande sysslor,
* dagliga och veckovisa uppgifter,
* poäng eller belöningssystem,
* historik,
* statistik,
* notifikationer,
* bilder eller ikoner för yngre barn,
* flera barn per syssla,
* deadlines,
* schemaläggning,
* PWA-installation,
* förbättrat surfplatteläge.

Dessa funktioner är möjliga framtida tillägg och ska inte betraktas som färdig funktionalitet.

---

# Plattform

Målet är att Syssloappen ska fungera som en responsiv webbapplikation.

Det gör att samma system kan användas från exempelvis:

* dator,
* surfplatta,
* telefon.

En PWA-lösning är också möjlig, vilket skulle göra att webbapplikationen kan installeras på exempelvis barnens surfplattor och användas mer som en vanlig app.

---

# Repository

Det här repositoryt innehåller källkoden och dokumentationen för Syssloappen.

Projektet utvecklas stegvis med Git och GitHub som versionshantering.

---

# Sammanfattning

Syssloappen handlar i grunden om en mycket enkel fråga:

> **Vem ska göra vad hemma — och är det gjort?**

Bakom den enkla frågan byggs ett fullständigt fullstacksystem med användare, roller, households, autentisering, API, databas och ett lättanvänt gränssnitt.

Målet är en applikation som är enkel för familjen att använda, men samtidigt tekniskt uppbyggd på ett sätt som gör att projektet kan växa vidare över tid.
