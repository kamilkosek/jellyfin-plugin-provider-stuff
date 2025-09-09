const https = require('https');
const crypto = require('crypto');
const fs = require('fs');

const repository = process.env.GITHUB_REPO; // e.g. kamilkosek/jellyfin-plugin-provider-stuff
const version = process.env.VERSION;        // e.g. 1.1.0
const file = process.env.FILE;              // e.g. providerstuff-1.1.0.zip
const targetAbi = '10.9.0.0';

const manifestPath = './manifest.json';
if (!fs.existsSync(manifestPath)) {
    console.error('manifest.json file not found');
    process.exit(1);
}

const jsonData = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));

const newVersion = {
    version,
    changelog: `- See the full changelog at [GitHub](https://github.com/${repository}/releases/tag/${version})\n`,
    targetAbi,
    sourceUrl: `https://github.com/${repository}/releases/download/${version}/${file}`,
    checksum: getMD5FromFile(),
    timestamp: new Date().toISOString().replace(/\.\d{3}Z$/, 'Z')
};

function getMD5FromFile() {
    const fileBuffer = fs.readFileSync(`./dist/${file}`);
    return crypto.createHash('md5').update(fileBuffer).digest('hex');
}

async function verifyChecksum(url, expectedChecksum) {
    return new Promise((resolve, reject) => {
        https.get(url, (res) => {
            if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
                // handle redirects
                return resolve(verifyChecksum(res.headers.location, expectedChecksum));
            }
            const hash = crypto.createHash('md5');
            res.on('data', (chunk) => hash.update(chunk));
            res.on('end', () => resolve(hash.digest('hex') === expectedChecksum));
        }).on('error', reject);
    });
}

async function validVersion(version) {
    console.log(`Validating version ${version.version}...`);
    const isValidChecksum = await verifyChecksum(version.sourceUrl, version.checksum);
    if (!isValidChecksum) {
        console.error(`Checksum mismatch for URL: ${version.sourceUrl}`);
        process.exit(1);
    } else {
        console.log(`Version ${version.version} is valid.`);
    }
}

async function updateManifest() {
    await validVersion(newVersion);

    jsonData[0].versions = jsonData[0].versions || [];
    jsonData[0].versions.unshift(newVersion);

    fs.writeFileSync(manifestPath, JSON.stringify(jsonData, null, 4));
    console.log('Manifest updated successfully.');
    process.exit(0);
}

updateManifest().catch((e) => {
    console.error(e);
    process.exit(1);
});
