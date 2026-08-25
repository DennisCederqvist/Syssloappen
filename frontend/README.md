# Syssloappen frontend

Mobile-first Angular 22-frontend med Tailwind CSS. Den första avgränsade versionen innehåller Adult-login, Child-enhetskoppling, Child-reservlogin, återställning av cookie-session och rollstyrda startsidor.

## Lokal utveckling

Starta API:t från repots rot:

```powershell
dotnet run --project backend/Syssloappen.Api --launch-profile http
```

Starta sedan frontend i en andra terminal:

```powershell
cd frontend
npm install
npm start
```

Angulars dev-server använder `proxy.conf.json` för att skicka `/api` till `http://localhost:5047`. Det gör att backends HttpOnly-sessioncookie kan användas via samma origin i webbläsaren.

## Verifiering

```powershell
npm run build
npm test -- --watch=false
```
