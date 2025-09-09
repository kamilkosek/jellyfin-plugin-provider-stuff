const fs = require('fs');

const VERSION = process.env.VERSION;

const csprojPath = './Jellyfin.Plugin.ProviderStuff/Jellyfin.Plugin.ProviderStuff.csproj';
if (!fs.existsSync(csprojPath)) {
    console.error('Jellyfin.Plugin.ProviderStuff.csproj file not found');
    process.exit(1);
}

fs.readFile(csprojPath, 'utf8', (err, data) => {
    if (err) {
        return console.error('Failed to read .csproj file:', err);
    }

    let newAssemblyVersion = null;
    let newFileVersion = null;

    const updatedData = data
        .replace(/<AssemblyVersion>(.*?)<\/AssemblyVersion>/, () => {
            newAssemblyVersion = VERSION;
            return `<AssemblyVersion>${newAssemblyVersion}</AssemblyVersion>`;
        })
        .replace(/<FileVersion>(.*?)<\/FileVersion>/, () => {
            newFileVersion = VERSION;
            return `<FileVersion>${newFileVersion}</FileVersion>`;
        });

    fs.writeFile(csprojPath, updatedData, 'utf8', (err) => {
        if (err) {
            return console.error('Failed to write .csproj file:', err);
        }
        console.log('Version updated successfully!');
    });
});
