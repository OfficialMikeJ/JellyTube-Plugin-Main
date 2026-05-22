# Jellyfin-AIO (All-in-One Plugin) Specification

This document serves as the master requirement file for the Jellyfin All-in-One plugin. 

## Project Architecture
The plugin is divided into four main tabs to ensure a clean, non-cluttered UI/UX. The interface must use a dark theme with modern, sleek styling (gradients, rounded corners, and smooth transitions).

---

## Tab 1: File Management (Direct Uploads)
**Goal:** Provide a robust way to upload media directly to the server through the Jellyfin web interface.

* **Supported Types:** Video, Music, and general media files.
* **Upload Interface:**
    * Form-based upload with a sleek Dark Theme.
    * Real-time progress bar showing completion percentage.
    * Network speed indicator (Mbps/Gbps).
* **Technical Features:**
    * **File Chunking:** Break large files into chunks to prevent timeout and allow resume.
    * **Bulk Uploading:** Support for selecting and uploading multiple files simultaneously.
    * **Configurable Limits:** Admin setting to change maximum allowed file sizes.

---

## Tab 2: JellyTube (YouTube 1:1 UI/UX)
**Goal:** A complete visual overhaul of the Jellyfin library view to mimic YouTube's layout exactly.

* **Styling:**
    * 1:1 YouTube color palette and font styling.
    * YouTube-style Sign-in buttons and Iconography.
* **Layout:**
    * **Collapsible Sidebar:** Left-side navigation that can be toggled.
    * **Video Cards:** 1:1 Thumbnail styling with the specific gradient shadow behind card titles.
    * **Recommendations:** A "Recommended" section styled exactly like the YouTube homepage.

---

## Tab 3: Media Requests (JellySeerr Replacement)
**Goal:** An internal media request system using Jellyfin's native authentication.

* **Integration:**
    * Login via existing Jellyfin Account (Server-locked: only users on this specific server can access).
* **Content Discovery:**
    * "Released" and "Coming Soon / In Theaters" lists.
    * Media posters and data pulled via **IMDB API**.
* **Request Flow:**
    * "Request Media" buttons for listed items.
    * Custom "Request Non-Listed Media" form for items not in the database.
* **User UI:**
    * **Top-Right Profile Menu:** Circle profile icon -> Clickable dropdown.
    * **Menu Items:** Profile, Preferences, Settings (Sub-menu: Password Reset, Sign-out, My Requests).
    * **Color Palette:** Buttons must be **Purple and Blue**.

---

## Tab 4: Statistics Dashboard (Insights & Analytics)
**Goal:** Detailed monitoring of user behavior and server network health.

* **Visuals:** Modern gauges, clean cards, and sleek graphs.
* **Data Points:**
    * **User Info:** Username + Public IP address.
    * **Watch Behavior:** Total watch time (Movies, TV, Documentaries), episode counts per user.
    * **Temporal Data:** Heatmaps or lists of when users watch (Monday-Sunday).
    * **Active Monitoring:** Real-time "What is currently being watched" and progress tracking.
    * **Network:** Real-time bandwidth and usage statistics.

---

## Implementation Guidelines
1.  **UI/UX:** Focus on a "clean and sleek" look. Avoid clutter by utilizing the tabbed navigation system.
2.  **Security:** Ensure the Media Request tab validates the session against the local Jellyfin database only.
3.  **Performance:** Upload chunking should be optimized for high-speed local and remote connections.