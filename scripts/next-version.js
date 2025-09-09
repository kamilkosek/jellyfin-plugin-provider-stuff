const { execSync } = require('child_process');

// Get the BUMP environment variable (major, minor, patch, build)
const bump = process.env.BUMP || 'build'; // default to bumping the last segment

function parseVersion(versionString) {
    const parts = versionString.split('.').map(Number);
    return parts.length >= 3 && parts.length <= 4 && parts.every(p => Number.isInteger(p) && p >= 0) ? parts : null;
}

function formatVersion(parts) {
    return parts.join('.');
}

function getLatestTag() {
    try {
        // Get all tags, filter to version-like tags, and sort them
        const output = execSync('git tag --list', { encoding: 'utf8' }).trim();
        if (!output) {
            return null;
        }
        
        const tags = output.split('\n');
        const versionTags = tags
            .map(tag => ({ tag, version: parseVersion(tag) }))
            .filter(item => item.version !== null)
            .sort((a, b) => {
                // Sort by version parts
                for (let i = 0; i < Math.max(a.version.length, b.version.length); i++) {
                    const aPart = a.version[i] || 0;
                    const bPart = b.version[i] || 0;
                    if (aPart !== bPart) {
                        return bPart - aPart; // descending
                    }
                }
                return 0;
            });
        
        return versionTags.length > 0 ? versionTags[0].version : null;
    } catch (error) {
        return null;
    }
}

function incrementVersion(version, bumpType) {
    const newVersion = [...version];
    
    // Ensure we have at least 4 parts for build bumping
    while (newVersion.length < 4) {
        newVersion.push(0);
    }
    
    switch (bumpType) {
        case 'major':
            newVersion[0]++;
            newVersion[1] = 0;
            newVersion[2] = 0;
            newVersion[3] = 0;
            break;
        case 'minor':
            newVersion[1]++;
            newVersion[2] = 0;
            newVersion[3] = 0;
            break;
        case 'patch':
            newVersion[2]++;
            newVersion[3] = 0;
            break;
        case 'build':
        default:
            newVersion[newVersion.length - 1]++;
            break;
    }
    
    return newVersion;
}

function main() {
    const latestVersion = getLatestTag();
    let nextVersion;
    
    if (!latestVersion) {
        // No tags found, start with 1.1.0.0
        nextVersion = [1, 1, 0, 0];
    } else {
        nextVersion = incrementVersion(latestVersion, bump);
    }
    
    const versionString = formatVersion(nextVersion);
    console.log(versionString);
}

if (require.main === module) {
    main();
}

module.exports = { parseVersion, formatVersion, getLatestTag, incrementVersion };