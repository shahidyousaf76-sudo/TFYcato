// Seeds default content into Firestore.
//   npm run seed:emulator            → local emulator (project demo-tfy-cat)
//   npm run seed -- --overwrite      → the project in GOOGLE_CLOUD_PROJECT using Application Default Credentials
//   npm run seed -- --make-super-admin you@example.com   → also grants the super_admin role to an existing email account
const args = process.argv.slice(2);
if (args.includes('--emulator')) {
  process.env.FIRESTORE_EMULATOR_HOST ??= '127.0.0.1:8080';
  process.env.FIREBASE_AUTH_EMULATOR_HOST ??= '127.0.0.1:9099';
  process.env.GCLOUD_PROJECT ??= 'demo-tfy-cat';
}

async function main() {
  const { seedContent } = await import('../src/admin/admin');
  const { auth } = await import('../src/util/firebase');
  const written = await seedContent(args.includes('--overwrite'));
  console.log(`Seeded ${written} documents.`);
  const i = args.indexOf('--make-super-admin');
  if (i >= 0 && args[i + 1]) {
    const user = await auth.getUserByEmail(args[i + 1]);
    await auth.setCustomUserClaims(user.uid, { role: 'super_admin' });
    console.log(`${args[i + 1]} is now super_admin (sign out and in again).`);
  }
}

main().then(() => process.exit(0)).catch((e) => { console.error(e); process.exit(1); });
