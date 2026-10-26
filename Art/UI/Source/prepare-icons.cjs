// Run with a local sharp installation: node prepare-icons.cjs <path-to-sharp>.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require(process.argv[2]);
async function run() {
    for (const name of ['arrow-up', 'arrow-down', 'gauge', 'zap', 'panel']) {
        const source = fs.readFileSync(path.join(__dirname, name + '.svg'), 'utf8').replace('currentColor', '#ffffff');
        await sharp(Buffer.from(source), { density: 384 }).resize(96, 96)
            .png().toFile(path.resolve(__dirname, '../../../Assets/Resources/UI/Icons', name + '.png'));
    }
}
run().catch(error => { console.error(error); process.exitCode = 1; });
