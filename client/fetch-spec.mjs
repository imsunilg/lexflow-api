// Fetches the live OpenAPI 3.1 document from a running LexFlow.Api instance and writes it to
// swagger.json alongside this script — nswag.json reads from that local file rather than a URL
// so `npm run generate` works offline against whatever spec snapshot was last fetched.
const apiUrl = process.env.LEXFLOW_API_URL ?? "http://localhost:5000";
const specUrl = `${apiUrl}/swagger/v1/swagger.json`;

const response = await fetch(specUrl);
if (!response.ok) {
  console.error(`Failed to fetch ${specUrl}: HTTP ${response.status}`);
  console.error("Is the API running? See client/README.md for the full workflow (dotnet run --project src/LexFlow.Api).");
  process.exit(1);
}

const spec = await response.text();
await import("node:fs/promises").then(fs => fs.writeFile(new URL("./swagger.json", import.meta.url), spec));
console.log(`Wrote ${specUrl} -> client/swagger.json`);
