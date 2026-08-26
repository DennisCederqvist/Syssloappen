# Syssloappen – User Stories och Acceptance Criteria

## 1. Projektöversikt

Syssloappen är en familjeorienterad applikation där vuxna kan skapa och tilldela sysslor till barn, och där barn kan logga in på sina egna enheter för att se och rapportera sina sysslor som utförda.

Varje familj ska fungera som en separat enhet, ett **Household**. Användare som tillhör ett Household får aldrig kunna läsa eller ändra information som tillhör ett annat Household.

### Planerad teknikstack

- **Frontend:** Angular
- **CSS/UI:** Tailwind CSS
- **Backend:** C# / ASP.NET Core Web API
- **Databas:** PostgreSQL eller SQL Server
- **Authentication:** ASP.NET Core Authentication
- **Framtida möjlighet:** Progressive Web App (PWA)

### Statusmarkering

- `[x]` betyder att kriteriet är implementerat, testat och mergat till `main`.
- `[ ]` betyder att kriteriet inte är implementerat eller endast är delvis uppfyllt.
- Breda kriterier lämnas okryssade tills hela formuleringen är uppfylld.

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

- [x] En vuxen ska kunna registrera ett konto.
- [x] Kontot ska kunna autentiseras på ett säkert sätt.
- [x] Lösenord får aldrig lagras i klartext.
- [x] En ny vuxen utan tidigare familjekoppling ska kunna skapa ett Household.
- [x] Den vuxna ska automatiskt kopplas till sitt nya Household.
- [x] Den vuxna ska få rollen `Adult`.

---

## US-002 – Användare kan logga in

**Som användare**
vill jag kunna logga in
så att systemet kan identifiera mig och visa rätt information.

### Acceptance Criteria

- [x] En användare ska kunna logga in med sina inloggningsuppgifter.
- [x] Felaktiga inloggningsuppgifter ska inte ge åtkomst.
- [x] Backend ska kunna identifiera den autentiserade användaren.
- [x] Backend ska kunna identifiera användarens roll.
- [x] Backend ska kunna identifiera vilket Household användaren tillhör.

---

## US-003 – Användare kan logga ut

**Som användare**
vill jag kunna logga ut
så att någon annan inte får tillgång till mitt konto.

### Acceptance Criteria

- [x] Det ska finnas en logout-funktion.
- [x] Efter logout ska skyddade delar av systemet inte längre vara tillgängliga.
- [x] Användaren ska behöva autentisera sig igen för att återfå åtkomst.

---

# 5. User Stories – Household

## US-010 – Vuxen kan skapa ett Household

**Som vuxen**
vill jag kunna skapa en familj
så att mina familjemedlemmar och vår data hålls separerade från andra familjer.

### Acceptance Criteria

- [x] Ett Household ska ha ett unikt ID.
- [x] Personen som skapar Household ska bli medlem i det.
- [x] Skaparen ska ha rollen `Adult`.
- [x] Data från andra Households får inte vara tillgänglig.

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

- [x] En Adult får endast se barn i sitt eget Household.
- [x] En Adult får endast administrera barn i sitt eget Household.
- [x] En Adult får endast skapa sysslor för sitt eget Household.
- [x] En Adult får endast tilldela sysslor till barn i sitt eget Household.
- [x] Ett Child får endast se information som tillhör sitt eget Household.
- [x] Backend ska kontrollera Household-tillhörighet.
- [x] Manipulering av ID:n i API-anrop får inte ge åtkomst till ett annat Households data.

---

# 6. User Stories – Barnkonton

## US-020 – Vuxen kan skapa barn

**Som vuxen**
vill jag kunna lägga till ett barn i min familj
så att barnet får ett konto och kan använda appen.

### Acceptance Criteria

- [x] Endast en Adult får skapa ett barn.
- [x] Barnet ska automatiskt kopplas till den vuxnas Household.
- [x] Barnet ska ha ett eget unikt ID.
- [x] Barnet ska inte kunna kopplas till ett annat Household genom ett modifierat API-anrop.
- [x] Alla Adults i samma Household ska kunna se barnet.
- [x] Barnprofilen och barnkontot ska skapas tillsammans i samma Adult-initierade flöde.
- [x] Skapandet ska vara atomärt så att varken en profil utan konto eller ett konto utan profil lämnas kvar vid fel.

---

## US-021 – Vuxen kan skapa barnets inloggning

**Som vuxen**
vill jag kunna skapa inloggningsuppgifter åt mitt barn
så att barnet kan logga in på sin egen enhet.

### Acceptance Criteria

- [x] En Adult ska kunna skapa ett användarkonto åt ett barn.
- [x] Barnkontot ska kopplas till rätt Child.
- [x] Barnkontot ska tillhöra samma Household som barnet.
- [x] Barnkontot ska få rollen `Child`.
- [x] Barnprofil och barnkonto ska skapas tillsammans; en Adult ska inte behöva skapa profilen först och kontot i ett separat steg.
- [x] Lösenordet ska hanteras av ASP.NET Core Identity och får aldrig lagras i klartext.
- [x] Child-konton ska inte behöva en egen e-postadress, medan Adult-konton fortsatt ska kräva unik e-post.
- [x] Barnets ordinarie enhet ska i första hand autentiseras genom en Adult-styrd engångskoppling.
- [x] En Adult får inte skapa login för ett barn i ett annat Household.
- [x] En Adult ska kunna välja ett barnvänligt användarnamn som är unikt inom det egna Householdet.
- [x] Jämförelse av barnets användarnamn ska vara skiftlägesokänslig, så att exempelvis `Markus` och `markus` räknas som samma namn inom ett Household.
- [x] Samma barnvänliga användarnamn ska kunna användas i olika Households utan globala namnvarianter som `markus17`.
- [x] Endast en autentiserad Adult ska kunna skapa en kopplingskod för ett aktivt barn i sitt eget Household.
- [x] Kopplingskoden ska vara slumpmässigt genererad, kortlivad och endast kunna användas en gång.
- [x] Backend ska binda kopplingskoden till exakt Child och Household; barnets enhet får inte välja ett rått `ChildId` eller `HouseholdId`.
- [x] Upprepade felaktiga försök att använda kopplingskoder ska begränsas.
- [x] En lyckad koppling ska skapa en beständig Child-session på enheten så att barnet normalt inte behöver logga in igen varje gång appen öppnas.
- [x] Sessionen ska ha en maximal livslängd och kunna förnyas säkert medan enheten används.
- [x] En Adult ska kunna återkalla barnets kopplade enheter, exempelvis om en enhet tappas bort.
- [x] Logout, återkallad enhetskoppling eller avaktivering av barnet ska göra den berörda sessionen ogiltig i backend.
- [x] Backend ska fortsätta verifiera att barnet och dess Household är aktiva även när en beständig session används.
- [x] Backend ska generera en unik familjekod som används tillsammans med barnets användarnamn och lösenord vid reservinloggning.
- [x] Familjekoden ska identifiera Householdet men ska inte behandlas som en ersättning för barnets lösenord.
- [x] Backend ska härleda `HouseholdId` från familjekoden; klienten får inte skicka eller välja ett rått `HouseholdId` vid reservinloggning.
- [x] Ett tekniskt Identity-användarnamn får skapas internt för global unikhet men ska inte behöva visas för barnet.
- [x] Felaktig familjekod, felaktigt användarnamn och felaktigt lösenord ska ge samma neutrala felmeddelande.
- [x] Upprepade misslyckade reservinloggningar ska begränsas eller leda till en tillfällig kontolåsning.

### Beslutad sessionspolicy för barnets enhet

- Den beständiga Child-sessionen gäller i sju dagar från koppling eller senaste säkra förnyelse.
- Aktivitet när mindre än ett dygn återstår förnyar sessionen med upp till sju dagar.
- Sessionen får aldrig förlängas förbi den absoluta maxgränsen på trettio dagar från den ursprungliga inloggningen.
- Efter sju dagars inaktivitet, trettio dagars total livslängd, logout, Adult-återkallning eller avaktivering krävs en ny kopplingskod eller reservinloggning.

---

## US-022 – Barn ser endast sin egen information

**Som barn**
vill jag bara se information som gäller mig
så att appen blir enkel och privat.

### Acceptance Criteria

- [x] Barnet ska endast se sina egna tilldelade sysslor.
- [x] Barnet ska inte kunna se syskonens privata vy.
- [x] Barnet ska inte kunna se barn från andra Households.
- [x] Barnet ska inte kunna ändra vilket ChildId som används för att få tillgång till någon annans data.
- [x] Backend ska identifiera barnet från den autentiserade användaren.

---

## US-023 – Vuxen kan ändra ett barn

**Som vuxen**
vill jag kunna ändra ett barns uppgifter
så att jag exempelvis kan rätta ett felstavat namn.

### Acceptance Criteria

- [x] Endast en Adult får ändra ett barn.
- [x] En Adult får endast ändra barn i sitt eget Household.
- [x] Barnets namn ska kunna ändras.
- [x] Ett tomt eller ogiltigt namn ska inte kunna sparas.
- [x] Barnets `HouseholdId` får aldrig kunna ändras av klienten.
- [x] Manipulering av ett Child-ID får inte ge åtkomst till barn i ett annat Household.
- [x] Alla Adults i samma Household ska kunna se den uppdaterade informationen.

---

## US-024 – Vuxen kan ta bort ett barn från aktiv användning

**Som vuxen**
vill jag kunna ta bort ett barn från familjens aktiva vy
så att barn som inte längre ska använda appen inte visas eller får nya sysslor.

### Acceptance Criteria

- [x] Endast en Adult får ta bort eller avaktivera ett barn.
- [x] En Adult får endast ta bort eller avaktivera barn i sitt eget Household.
- [x] Manipulering av ett Child-ID får inte påverka barn i ett annat Household.
- [x] Ett avaktiverat barn ska inte visas bland Householdets aktiva barn.
- [x] Ett avaktiverat barn ska inte kunna få nya sysslor.
- [x] Ett eventuellt kopplat barnkonto ska inte längre kunna logga in.
- [ ] Historiska tilldelningar, godkännanden och completions ska bevaras.
- [x] Borttagning ska därför normalt implementeras som avaktivering, inte fysisk radering av databasposten.

---

# 7. User Stories – Sysslor

## US-030 – Vuxen kan skapa en syssla

**Som vuxen**
vill jag kunna skapa en syssla
så att den senare kan tilldelas ett barn.

### Acceptance Criteria

- [x] Endast en Adult får skapa sysslor.
- [x] Sysslan ska ha ett namn.
- [x] En Adult ska kunna välja om sysslan är värd `5`, `10`, `15` eller `20` poäng.
- [x] Om inget poängvärde anges ska backend använda `5` poäng.
- [x] Andra poängvärden ska nekas av backend.
- [x] Sysslan ska kopplas till den vuxnas Household.
- [x] En syssla från ett Household får inte vara synlig i ett annat Household.
- [x] Systemet ska spara vem som skapade sysslan.
- [x] En sparad syssla ska fungera som en återanvändbar mall i Householdets uppgiftsbank och kunna tilldelas flera gånger utan att behöva skapas på nytt.
- [x] Efter att en syssla skapats ska den vuxna kunna tilldela den direkt, men tilldelning ska inte krävas för att spara mallen.

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

- [x] Endast en Adult får tilldela sysslor.
- [x] Sysslan och barnet måste tillhöra samma Household.
- [x] Den vuxna måste tillhöra samma Household som barnet.
- [x] Tilldelningen ska sparas i databasen.
- [x] Tilldelningen ska spara sysslans poängvärde när den skapas, så att senare ändringar inte påverkar redan tilldelade sysslor.
- [x] Barnet ska kunna se tilldelningen efter inloggning.
- [x] Det ska inte gå att tilldela en syssla till ett barn från ett annat Household.

---

## US-032 – Vuxen kan se familjens tilldelade sysslor

**Som vuxen**
vill jag kunna se familjens aktuella sysslor
så att jag vet vem som ska göra vad.

### Acceptance Criteria

- [x] Adults ska kunna se Householdets barn.
- [x] Adults ska kunna se vilka sysslor varje barn har.
- [x] Information från andra Households får inte visas.

---

## US-033 – Vuxen kan administrera uppgiftsbanken

**Som vuxen**
vill jag kunna ändra och plocka bort sparade sysslor
så att uppgiftsbanken förblir aktuell och enkel att använda.

### Acceptance Criteria

- [x] Endast en autentiserad Adult får ändra eller avaktivera en syssla.
- [x] En Adult får endast administrera sysslor i sitt eget Household.
- [x] Titel, valfri beskrivning och poängvärde ska kunna ändras.
- [x] Endast poängvärdena `5`, `10`, `15` och `20` får sparas.
- [x] Ett ändrat poängvärde ska endast påverka framtida tilldelningar; redan skapade tilldelningar ska behålla sitt snapshot-värde.
- [x] Varje kort i uppgiftsbanken ska ha ett litet kryss i övre hörnet för att plocka bort sysslan.
- [x] Kryssknappen ska ha ett tydligt tillgängligt namn och avaktivering ska kräva bekräftelse för att undvika misstag.
- [x] En bortplockad syssla ska döljas från uppgiftsbanken och inte kunna användas för nya tilldelningar.
- [x] Borttagning ska implementeras som avaktivering i backend så att historiska tilldelningar, completions och poäng bevaras.
- [x] Manipulering av Chore-ID eller Household-fält får inte ändra eller avaktivera ett annat Households syssla.

---

## US-034 – Vuxen kan avbryta en felaktig tilldelning

**Som vuxen**
vill jag kunna avbryta en syssla som tilldelats fel
så att fel barn inte behöver se eller utföra uppgiften.

### Acceptance Criteria

- [x] Endast en autentiserad Adult får avbryta en tilldelning.
- [x] En Adult får endast avbryta tilldelningar i sitt eget Household.
- [x] Tilldelningar med status `Assigned`, `PendingApproval` eller `NeedsRedo` ska kunna avbrytas.
- [x] En `Approved` tilldelning får inte avbrytas eftersom completion och poäng redan har skapats.
- [x] Avbrytning ska sätta status `Cancelled`, spara vilken Adult som avbröt och aktuell backendtid.
- [x] Tilldelningen får inte raderas fysiskt; historik och eventuella tidigare rapporterings- eller granskningsuppgifter ska bevaras.
- [x] En avbruten tilldelning ska döljas från barnets aktiva sysslelista och får inte kunna rapporteras som utförd.
- [x] Adult ska kunna begära historik som även innehåller avbrutna tilldelningar.
- [x] Adult-frontendens aktuella lista ska ta bort tilldelningen direkt efter lyckad avbrytning.
- [x] Avbrytning i frontend ska kräva bekräftelse och knappen ska ha ett tydligt tillgängligt namn.
- [x] Manipulering av Assignment-ID, Child-ID, Household-, status-, Adult- eller tidsfält får inte avbryta en annan familjs tilldelning eller styra auditinformationen.

---

# 8. User Stories – Barnets vy

## US-040 – Barn kan se sina sysslor

**Som barn**
vill jag kunna se mina aktuella sysslor
så att jag vet vad jag ska göra.

### Acceptance Criteria

- [x] Barnet måste vara inloggat.
- [x] Barnet ska endast se sysslor som är tilldelade det aktuella barnet.
- [x] Barnet ska kunna se om en syssla är tilldelad, väntar på godkännande, behöver göras om eller är godkänd.
- [x] Sysslor som tillhör syskon eller andra familjer får inte visas.

### Exempel

```text
Mina sysslor

[ ] Mata katten
[ ] Städa rummet
[x] Ta ut soporna
```

---

## US-041 – Barn kan rapportera en syssla som utförd

**Som barn**
vill jag kunna rapportera en syssla som utförd
så att en vuxen kan kontrollera och godkänna arbetet.

### Acceptance Criteria

- [x] Barnet måste vara inloggat.
- [x] Barnet får endast rapportera sina egna tilldelningar som utförda.
- [x] Ett barn får inte rapportera ett syskons syssla som utförd.
- [x] Tilldelningen ska få status `PendingApproval`.
- [x] Sysslan ska inte räknas som godkänd direkt.
- [x] En slutgiltig completion ska inte skapas innan en Adult har godkänt sysslan.
- [x] Inga eventuella poäng får delas ut innan en Adult har godkänt sysslan.
- [x] Systemet ska spara vilket barn som utförde sysslan.
- [x] Systemet ska spara vilken syssla som utfördes.
- [x] Systemet ska spara tidpunkten då barnet rapporterade sysslan som utförd.

---

# 9. User Stories – Vuxenvy

## US-050 – Vuxen kan se utförda sysslor

**Som vuxen**
vill jag kunna se vilka sysslor barnen har gjort
så att jag kan följa deras aktivitet.

### Acceptance Criteria

- [x] Adults ska kunna se utförda sysslor inom sitt Household.
- [x] Det ska framgå vilket barn som gjorde sysslan.
- [x] Det ska framgå vilken syssla som utfördes.
- [x] Det ska framgå när barnet rapporterade sysslan som utförd.
- [x] För godkända sysslor ska det framgå vem som godkände dem och när.
- [x] Information från andra Households får inte visas.

### Exempel

```text
Senaste aktiviteter

✓ Anna – Mata katten – 07:42
✓ Erik – Töm diskmaskinen – 08:15
✓ Anna – Städa rummet – 10:32
```

---

## US-051 – Vuxen godkänner eller nekar en utförd syssla

**Som vuxen**
vill jag kunna kontrollera en syssla som barnet har rapporterat som utförd
så att endast korrekt utförda sysslor blir godkända.

### Acceptance Criteria

- [x] Endast en Adult får godkänna eller neka en rapporterad syssla.
- [x] En Adult ska kunna se vilka sysslor inom sitt Household som väntar på godkännande.
- [x] Den vuxna, barnet och sysslan måste tillhöra samma Household.
- [x] En Adult får inte granska sysslor från ett annat Household.
- [x] Vid godkännande ska tilldelningens status ändras till `Approved`.
- [x] En completion ska skapas först när sysslan godkänns.
- [x] Systemet ska spara vilken Adult som godkände sysslan och när.
- [x] Eventuella poäng får delas ut först efter godkännande.
- [x] Vid nekande ska tilldelningens status ändras till `NeedsRedo`.
- [x] Barnet ska kunna se att sysslan behöver göras om.
- [x] Den vuxna ska valfritt kunna lämna en kommentar.

### Statusflöde

```text
Assigned
   ↓ barnet rapporterar utförd
PendingApproval
   ↓ Adult granskar
Approved eller NeedsRedo
```

---

# 10. User Stories – Poäng och belöningar

## US-060 – Barn får poäng för godkända sysslor

**Som barn**
vill jag få poäng när en vuxen godkänner en utförd syssla
så att jag kan se resultatet av mitt arbete.

### Acceptance Criteria

- [x] Poängvärdet ska bestämmas av en Adult när sysslan skapas.
- [x] Tillåtna poängvärden ska vara `5`, `10`, `15` och `20`.
- [x] Standardvärdet ska vara `5` poäng.
- [x] Barnet ska kunna se hur många poäng en tilldelning är värd.
- [x] Inga poäng får delas ut när barnet endast rapporterar sysslan som utförd.
- [x] Inga poäng får delas ut när en Adult markerar sysslan som `NeedsRedo`.
- [x] Poäng ska delas ut först när en Adult godkänner sysslan.
- [x] Godkännandet ska spara exakt hur många poäng som delades ut.
- [x] Samma tilldelning får aldrig dela ut poäng mer än en gång.
- [x] Barnets totala poäng ska kunna beräknas från godkända completions.
- [x] Klienten får inte själv välja utdelade poäng vid godkännandet.
- [x] Poäng och completions ska vara strikt isolerade per Household.

---

## US-070 – Vuxen skapar familjens belöningar

**Som vuxen**
vill jag kunna lägga till belöningar som barnet kan använda sina poäng till
så att familjen själv kan bestämma vad poängen betyder.

### Acceptance Criteria

- [ ] Endast en autentiserad Adult får skapa och administrera belöningar.
- [ ] En belöning ska ha ett namn, exempelvis `Litet gosedjur` eller `Lite godis`.
- [ ] En belöning ska ha ett positivt poängpris i heltal.
- [ ] En belöning ska kunna ha en valfri beskrivning.
- [ ] En Adult ska valfritt kunna lägga till en bild till belöningen.
- [ ] Belöningsflödet ska fungera även utan bild.
- [ ] Bildformat, filstorlek och lagring ska valideras och beslutas innan bilduppladdning implementeras.
- [ ] Belöningen ska automatiskt kopplas till den autentiserade Adult-användarens Household.
- [ ] Klienten får inte styra Household, skapare, ID eller tidsuppgifter.
- [ ] En Adult får endast se och administrera belöningar i sitt eget Household.
- [ ] En belöning ska kunna avaktiveras utan att historiska köp eller förfrågningar raderas.

---

## US-071 – Barn begär att använda poäng till en belöning

**Som barn**
vill jag kunna välja en belöning från min familjs lista
så att jag kan använda mina intjänade poäng.

### Acceptance Criteria

- [ ] Barnet måste vara autentiserat och aktivt.
- [ ] Barnet ska endast se aktiva belöningar från sitt eget Household.
- [ ] Barnet ska se belöningens namn, poängpris, valfria beskrivning och eventuell bild.
- [ ] Tillgängliga poäng ska beräknas som intjänade poäng minus reserverade och slutligt använda poäng.
- [ ] Backend ska kontrollera att barnet har tillräckligt många tillgängliga poäng.
- [ ] En godkänd begäran ska reservera poängen direkt så att samma poäng inte kan användas flera gånger samtidigt.
- [ ] Backend ska skapa en beständig redemption med status `Requested`.
- [ ] Redemption ska spara belöningen, barnet, Household, det aktuella poängpriset och begäranstiden.
- [ ] Det sparade poängpriset ska vara en snapshot som inte ändras om belöningens pris senare ändras.
- [ ] Klienten får inte styra Child, Household, poängpris, status eller tidsuppgifter.
- [ ] Otillräckliga poäng, avaktiverade belöningar och upprepade eller manipulerade anrop ska hanteras atomärt och säkert.
- [ ] Ett barn får inte se eller begära ett syskons eller ett annat Households belöningar.

---

## US-072 – Vuxen hanterar barnets belöningsförfrågan

**Som vuxen**
vill jag kunna godkänna, lämna ut eller avbryta en belöningsförfrågan
så att familjens fysiska belöningar hanteras kontrollerat.

### Acceptance Criteria

- [ ] Endast en autentiserad Adult i samma Household får hantera förfrågan.
- [ ] En Adult ska kunna se Householdets förfrågningar med barn, belöning, poängpris och status.
- [ ] En `Requested`-förfrågan ska kunna ändras till `Approved` eller `Cancelled`.
- [ ] En `Approved`-förfrågan ska kunna markeras som `Delivered` när belöningen lämnats ut.
- [ ] En förfrågan som avbryts före utlämning ska frigöra de reserverade poängen.
- [ ] En `Delivered`-förfrågan ska inte kunna avbrytas eller återbetalas genom ett manipulerat anrop.
- [ ] Systemet ska spara vilken Adult som hanterade förfrågan och när.
- [ ] Den vuxna ska valfritt kunna lämna en kommentar.
- [ ] Samma förfrågan får inte dra av, frigöra eller återbetala poäng flera gånger.
- [ ] Samtidiga anrop får inte kunna skapa negativt saldo eller dubbel användning av poäng.
- [ ] Historiken ska bevaras även om barnet eller belöningen senare avaktiveras.
- [ ] Information och ändringar ska vara strikt isolerade per Household.

---

# 11. Behörighetsmatris

| Funktion                        | Adult           | Child |
| ------------------------------- | --------------- | ----- |
| Logga in                        | Ja              | Ja    |
| Logga ut                        | Ja              | Ja    |
| Se egna sysslor                 | Ja\*            | Ja    |
| Se familjens barn               | Ja              | Nej   |
| Skapa barn                      | Ja              | Nej   |
| Ändra barn                      | Ja              | Nej   |
| Avaktivera barn                 | Ja              | Nej   |
| Skapa barnkonto                 | Ja              | Nej   |
| Skapa syssla                    | Ja              | Nej   |
| Tilldela syssla                 | Ja              | Nej   |
| Rapportera egen syssla utförd   | Nej/ej relevant | Ja    |
| Godkänna eller neka syssla      | Ja              | Nej   |
| Se familjens completions        | Ja              | Nej   |
| Skapa och administrera belöning | Ja              | Nej   |
| Begära belöning                 | Nej             | Ja    |
| Hantera belöningsförfrågan      | Ja              | Nej   |
| Bjuda in Adult                  | Ja              | Nej   |
| Administrera annat Household    | Nej             | Nej   |

- Adult-vyn behöver inte nödvändigtvis använda samma typ av tilldelning som Child-vyn.

---

# 12. Föreslagen datamodell

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
Points
IsActive
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
Points
Status
SubmittedAt
ReviewedByUserId
ReviewedAt
ReviewComment
```

## ChoreCompletion

```text
Id
HouseholdId
AssignmentId
ChildId
ChoreId
ApprovedByUserId
ApprovedAt
PointsAwarded
```

## Reward

```text
Id
HouseholdId
CreatedByUserId
Name
Description
PointsCost
ImageReference
IsActive
CreatedAt
```

## RewardRedemption

```text
Id
HouseholdId
RewardId
ChildId
PointsCost
Status
RequestedAt
ReviewedByUserId
ReviewedAt
DeliveredAt
Comment
```

---

# 13. Viktiga backend-regler

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

För godkännandeflödet gäller dessutom:

- Ett Child får endast rapportera sina egna tilldelningar som utförda.
- Endast en Adult i samma Household får godkänna eller neka arbetet.
- En completion och eventuella poäng får skapas först efter godkännande.

---

# 14. Frontend och användarupplevelse

Den första frontendversionen får vara visuellt enkel. Målet är först att göra de färdiga backendflödena begripliga och användbara. Färger, illustrationer och mer avancerad design ska kunna ändras senare utan att affärslogiken behöver byggas om.

### Acceptance Criteria

- [x] Frontend ska byggas i Angular.
- [x] Frontend ska utformas mobile first eftersom både Adults och Children främst väntas använda mobil eller surfplatta.
- [ ] Adult-flödena ska vara fullt användbara på en vanlig mobilskärm utan horisontell scrollning.
- [x] Child-flödena ska vara fullt användbara på både mobil och surfplatta utan horisontell scrollning.
- [ ] Primära knappar och val ska ha tydliga texter och vara lätta att trycka på med fingret.
- [ ] Text, status, felmeddelanden och poäng ska vara tydligt läsbara på små skärmar.
- [x] Adult- och Child-vyer ska vara separerade och anpassade efter respektive roll.
- [x] Desktoplayout får förbättras responsivt men ska inte prioriteras före mobilflödena.
- [x] Den första designen ska vara enkel och komponentbaserad så att utseendet kan ändras senare.
- [x] Skapa-syssla-formuläret ska ha en titelruta och en poängrullista med `5`, `10`, `15` och `20`, där `5` är förvalt.
- [x] Frontend får aldrig ersätta backendens kontroller av identitet, roll, Household, status, poäng eller ägarskap.
- [ ] Grundläggande tillgänglighet ska beaktas, inklusive formuläretiketter, tangentbordsnavigering, fokusmarkering och tillräckliga kontraster.

---

# 15. MVP

Första fungerande versionen ska vara liten.

## Ska ingå

- [x] Adult kan skapa konto.
- [x] Adult kan skapa ett Household.
- [x] Adult kan skapa barn.
- [x] Adult kan ändra barn.
- [x] Adult kan avaktivera barn.
- [x] Adult kan skapa login åt barn.
- [x] Adult kan skapa sysslor.
- [x] Adult kan tilldela sysslor.
- [x] Child kan logga in genom Adult-styrd enhetskoppling.
- [x] Child kan se sina egna sysslor.
- [x] Child kan rapportera en syssla som utförd.
- [x] Adult kan godkänna eller neka en rapporterad syssla.
- [x] Adult kan se utförda sysslor.
- [x] Household-isolering fungerar.
- [x] Authentication och authorization fungerar.

---

# 16. Inte MVP

Följande funktioner kan vara intressanta senare men ska **inte byggas innan kärnfunktionerna fungerar**.

- Belöningsbutik och belöningsförfrågningar enligt US-070–US-072
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

# 17. Möjliga framtida funktioner

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

## Belöningsbutik

Poäng för godkända sysslor är implementerade. Nästa framtida del är familjedefinierade belöningar och belöningsförfrågningar enligt US-070–US-072.

```text
Lite godis          25 poäng
Liten leksak       100 poäng
Gosedjur           200 poäng
```

Barnet ska senare kunna använda poängen för familjedefinierade belöningar utan att samma poäng kan användas flera gånger.

---

## PWA

Angular-applikationen kan senare göras till en Progressive Web App.

Målet är att barnet ska kunna installera appen på sin surfplatta och få en ikon ungefär som en vanlig app.

PWA-arbetet bör göras **efter att webbversionens kärnfunktionalitet fungerar**.

---

## QR-kod för barnets enhetskoppling

Den Adult-styrda enhetskopplingen med engångskod ingår i barnloginens kärnflöde. Som en framtida användarvänlig förbättring ska samma kopplingsflöde även kunna startas genom att barnets enhet skannar en QR-kod i stället för att den vuxna skriver in engångskoden manuellt.

- QR-koden ska representera samma slumpmässiga, kortlivade engångstoken som den manuella kopplingskoden.
- Token ska fortfarande verifieras i backend, bara kunna användas en gång och vara bunden till rätt Child och Household.
- QR-koden ska inte innehålla barnets lösenord, ett rått `HouseholdId` eller någon permanent inloggningshemlighet.
- Enhetskopplingen ska inte ge åtkomst till ett barn i ett annat Household eller till ett avaktiverat barn.
- Manuell kopplingskod och reservinloggning ska finnas kvar när enheten saknar kamera eller QR-skanning inte fungerar.
- QR-skanning ska implementeras först efter att den manuella enhetskopplingen och webbversionens kärnflöde fungerar säkert.

---

# 18. Development Guidelines för AI-assisterad utveckling

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

# 19. Definition of Done

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

# 20. Prioriterad fortsatt implementation

Backendens kärnflöde för autentisering, barn, sysslor, tilldelning, rapportering, Adult-granskning och intjänade poäng är färdigt. Angular-grunden, Adult-registrering och login, rollstyrd navigation samt Adult-hantering av barnkonton och kopplade enheter är också färdiga. Föreslagen fortsatt ordning är:

```text
1. Child-frontend för egna sysslor och status enligt US-040
   ↓
2. Child-frontend för rapportering, kommentar och poängsaldo enligt US-041 och US-060
   ↓
3. Adult-frontend för PendingApproval, Approved och NeedsRedo enligt US-050 och US-051
   ↓
4. Browserbaserade end-to-end-tester av hela syssleflödet
   ↓
5. Responsivitet, tillgänglighet och browserbaserade kärnflödestester
   ↓
6. Ytterligare Adult enligt US-011
   ↓
7. Belöningskatalogens backend enligt US-070
   ↓
8. Säker poängreservation och redemption enligt US-071–US-072
   ↓
9. Mobile-first belöningsbutik utan krav på bild
   ↓
10. Säker bilduppladdning för belöningar
   ↓
11. PWA
```

---

# 21. Projektets kärnprincip

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

---

# 22. Verifierad Child-frontend

Child-frontenden för US-040, US-041 och poängpresentationen i US-060 är implementerad och automatiskt verifierad på branchen `feature/child-chores-and-submission`.

- Barnets startsida hämtar privata tilldelningar och poängsaldo från backend.
- `Assigned`, `PendingApproval`, `NeedsRedo` och `Approved` visas med begripliga svenska texter.
- Adult-kommentaren visas för `NeedsRedo`, som kan rapporteras igen.
- Rapportering skickar endast tilldelnings-ID i URL:en och ingen ägar-, status-, poäng- eller tidsdata.
- Loading-, fel- och tomlägen samt stora, tillgängligt namngivna tryckytor ingår.
- Frontendtester, formatteringskontroll, Angular-produktionsbygge, hela backendsviten och ett riktigt PostgreSQL/API-smoke-test är godkända.

Child-vyn användartestades 2026-08-26 med en riktig Adult-tilldelning och Child-session. Tilldelningen visades och kunde rapporteras till `PendingApproval`. Den tillfälligt gamla vyn visade sig komma från en stale Angular-devserver; efter kontrollerad omstart serverades och verifierades den nya Child-chunken. Användaren godkände därefter merge till `main`, som genomfördes och pushades i merge-commit `7a0fec8`. Feature-branchen raderades lokalt och på GitHub.

---

# 23. Verifierad Adult-granskning

Adult-frontenden för US-050 och US-051 är implementerad och automatiskt verifierad på branchen `feature/adult-chore-review`.

- Routens `/vuxen/granska` privata kö visar endast rapporterade tilldelningar med status `PendingApproval`.
- Varje kort visar barn, syssla, snapshot-poäng och rapporteringstid.
- En Adult kan godkänna och dela ut poäng eller välja `NeedsRedo` med en valfri kommentar.
- Frontenden skickar endast den valfria kommentaren; backend bestämmer Household, Adult, status, granskningstid och poäng.
- Dubbelklick och samtidiga review-anrop blockeras, och lyckade svar flyttar kortet direkt till granskningshistoriken.
- Loading-, fel-, retry- och tomlägen samt stora, tillgängligt namngivna tryckytor ingår.
- Angular-tester, formatteringskontroll, produktionsbygge, hela backendsviten och ett riktigt PostgreSQL/API-smoke-test är godkända.

Adult-granskningen användartestades 2026-08-26 med en riktig rapporterad Child-tilldelning. `NeedsRedo` med kommentar visades korrekt för barnet, och ett senare godkännande delade ut poängen. Barnets redan öppna vy hämtar inte förändringar i realtid utan visar det aktuella saldot efter en siduppdatering. Användaren godkände därefter merge till `main`, som genomfördes och pushades i merge-commit `df0ead7`.

---

# 24. Browserbaserat end-to-end-test

Det centrala syssleflödet har ett riktigt Playwright-test på branchen `feature/chore-flow-e2e`.

- Testet kör Chromium med mobil viewport och separata browser-contexts för Adult och Child, vilket ger isolerade sessionscookies som på två enheter.
- Hela flödet går genom Angular-gränssnittet och dess riktiga API-anrop: Adult-registrering och login, barnskapande, engångskod, syssla, tilldelning, Child-rapportering, `NeedsRedo` med kommentar, omrapportering, Adult-godkännande och uppdaterat Child-poängsaldo.
- Testet verifierar att saldot är `0` före godkännande och `10` efter godkännande samt att Adult-kommentaren visas för barnet.
- API och Angular startas automatiskt av Playwright-konfigurationen. PostgreSQL-databasen används på riktigt och testdata får unika E2E-märkta namn.
- `npm run e2e` passerar tillsammans med 53/53 Angular-tester, Angular-produktionsbygge, formatteringskontroll och 106/106 backendtester i Release.

Kontrollpunkten godkändes av användaren och mergades till `main` i merge-commit `1cba8f3`. Nästa planerade arbetsdel är en bredare responsivitets- och tillgänglighetsgenomgång av kärnflödena.

---

# 25. Verifierad responsivitets- och tillgänglighetsgenomgång

En bred genomgång av kärnflödena är implementerad och automatiskt verifierad på branchen `feature/responsive-accessibility-audit`.

- Login, registrering, Adult-startsidan, barn och konton, sysslor och tilldelningar, Adult-granskning samt Child-startsidan och sysslekorten ingår i browsergranskningen.
- Playwright verifierar vyerna vid `390 × 844`, `768 × 1024` och `1280 × 900` utan oavsiktlig horisontell scroll och med minst 44 × 44 px stora synliga knapp- och länkytor.
- Axe kör WCAG 2 A/AA och WCAG 2.1 A/AA i centrala tillstånd genom det riktiga Adult-/Child-flödet. Färgpalettens brand-, lilac- och muted-toner har justerats efter uppmätta kontrastfel.
- Varje sida har ett `main`-landmark och en primär `h1`. Långa sidrubriker får radbrytas i stället för att klippas, och Adult-tilldelningar visar begripliga svenska statusar även på mobil.
- Formulärens valideringsmeddelanden är kopplade till relevanta fält med `aria-invalid` och `aria-describedby`. Ogiltig submit flyttar fokus till det första fält som behöver rättas.
- Dialogliknande inline-paneler och bekräftelseflöden har namngivna regioner/grupper, fokus flyttas till nytt innehåll och återställs eller flyttas till resultatmeddelandet när innehåll stängs eller tas bort.
- Fokusmarkeringen har hög kontrast, uppgiftsbankens tidigare 32 px stora kryss är nu 48 px och dynamiska resultat behåller ett rimligt fokus efter statusövergångar.
- `prefers-reduced-motion: reduce` kortar animationer och övergångar globalt. E2E-körningen verifierar reduced-motion-läget tillsammans med loading-, status- och kärnflödestillstånden.
- Frontendsviten omfattar nu 55 godkända Angular-tester. Formatteringskontroll, Angular-produktionsbygge, Playwright-E2E mot riktigt API/PostgreSQL och 106/106 backendtester i Release är godkända.

De breda checkboxarna i avsnitt 14 förblir enligt dokumentets statusregel okryssade tills den testade branchen har användargodkänts och mergats till `main`.
