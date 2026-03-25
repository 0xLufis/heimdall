import postgres from 'postgres';
import fs from 'fs';
import path from 'path';
import crypto from 'node:crypto';

// Get database URL from environment or fallback to local dev string
const DATABASE_URL = process.env.DATABASE_URL || "postgresql://drizzle_admin:migrate@localhost:5432/heimdall_dev_db?sslmode=no-verify";

const sql = postgres(DATABASE_URL, {
  ssl: {
    rejectUnauthorized: false
  }
});

async function seed() {
  try {
    const dataPath = path.join(__dirname, '../seed_data/inventory.json');
    const data = JSON.parse(fs.readFileSync(dataPath, 'utf8'));

    console.log("Seeding Manufacturers...");
    const manufacturers = new Map();
    for (const m of data.manufacturers) {
      const [record] = await sql`
        INSERT INTO backend.manufacturers (id, name, website, support_contact)
        VALUES (${crypto.randomUUID()}, ${m.name}, ${m.website || null}, ${m.support_contact || null})
        ON CONFLICT (name) DO UPDATE SET website = EXCLUDED.website, support_contact = EXCLUDED.support_contact
        RETURNING id, name
      `;
      manufacturers.set(m.name, record.id);
      console.log(`- ${m.name}`);
    }

    console.log("\nSeeding Suppliers...");
    const suppliers = new Map();
    for (const s of data.suppliers) {
      const [record] = await sql`
        INSERT INTO backend.suppliers (id, name, contact_person, email)
        VALUES (${crypto.randomUUID()}, ${s.name}, ${s.contactPerson}, ${s.email})
        ON CONFLICT (name) DO UPDATE SET contact_person = EXCLUDED.contact_person, email = EXCLUDED.email
        RETURNING id, name
      `;
      suppliers.set(s.name, record.id);
      console.log(`- ${s.name}`);
    }

    console.log("\nSeeding Hardware Components...");
    for (const h of data.hardware) {
      const mId = manufacturers.get(h.manufacturer);
      const sId = suppliers.get(h.supplier);

      if (!mId || !sId) {
        console.warn(`Skipping ${h.name}: Manufacturer or Supplier not found.`);
        continue;
      }

      await sql`
        INSERT INTO backend.hardware_components (
          id, name, manufacturer_id, supplier_id, description, 
          serial_number, purchase_date, cost_in_huf, technical_specs
        ) VALUES (
          ${crypto.randomUUID()}, ${h.name}, ${mId}, ${sId}, ${h.description}, 
          ${h.serialNumber}, ${h.purchaseDate}, ${h.costInHUF}, ${h.technicalSpecs}
        )
      `;
      console.log(`- ${h.name}`);
    }

    console.log("\nSeeding Software Components...");
    for (const s of data.software) {
      const mId = manufacturers.get(s.manufacturer);
      const sId = suppliers.get(s.supplier);

      if (!mId || !sId) {
        console.warn(`Skipping ${s.name}: Manufacturer or Supplier not found.`);
        continue;
      }

      await sql`
        INSERT INTO backend.software_components (
          id, name, manufacturer_id, supplier_id, description, 
          serial_number, purchase_date, cost_in_huf, version
        ) VALUES (
          ${crypto.randomUUID()}, ${s.name}, ${mId}, ${sId}, ${s.description}, 
          ${s.serialNumber}, ${s.purchaseDate}, ${s.costInHUF}, ${s.version}
        )
      `;
      console.log(`- ${s.name}`);
    }

    console.log("\nSeeding Complete! 🚀");
    process.exit(0);
  } catch (error) {
    console.error("Seeding failed:", error);
    process.exit(1);
  }
}

seed();
