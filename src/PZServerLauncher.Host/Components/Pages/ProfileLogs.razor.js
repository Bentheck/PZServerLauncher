export async function getRecentLogs(profileId) {
    const response = await fetch(`/api/profiles/${encodeURIComponent(profileId)}/logs/recent`, {
        credentials: "same-origin",
        headers: {
            Accept: "application/json"
        }
    });

    if (!response.ok) {
        throw new Error(`Unable to load recent logs (${response.status})`);
    }

    return await response.json();
}
