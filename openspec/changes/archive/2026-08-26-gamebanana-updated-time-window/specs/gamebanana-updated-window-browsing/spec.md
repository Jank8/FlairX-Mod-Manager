## ADDED Requirements

### Requirement: Eligible character-skin browsing exposes separate range and ordering controls
The system SHALL expose a Time Range selector and a distinct Sort Within Range selector when Character Skins, primary Latest Updated order, and an empty search are active. The Time Range selector SHALL offer 30 days, 90 days, 180 days, and Unlimited and SHALL default to 90 days. The Sort Within Range selector SHALL offer Latest Updated, Most Liked, Most Downloaded, and Most Commented.

#### Scenario: Entering eligible mode
- **WHEN** the user enters Character Skins with Latest Updated and no search and no compatible navigation state is being restored
- **THEN** the system selects the 90-day range and Latest Updated local order and displays both clearly distinguished controls

#### Scenario: Leaving eligible mode
- **WHEN** the user leaves Character Skins, selects another primary order, or enters a non-empty search
- **THEN** the system hides the time-window controls and uses the existing loading behavior for that mode

### Requirement: Bounded windows use the GameBanana update timestamp
The system SHALL determine time-window membership exclusively from the GameBanana `_tsDateUpdated` value mapped to `DateUpdated`. It SHALL include records whose update timestamp equals the cutoff and SHALL NOT substitute publication or modification timestamps when the update timestamp is absent or zero.

#### Scenario: Record on the cutoff
- **WHEN** a record's `_tsDateUpdated` equals the selected range cutoff
- **THEN** the system includes the record in the candidate pool

#### Scenario: Publication is recent but update timestamp is old
- **WHEN** a record has a publication timestamp inside the window but `_tsDateUpdated` is older than the cutoff
- **THEN** the system excludes the record

#### Scenario: Update timestamp is missing
- **WHEN** a finite time window is active and a record has a missing or zero `_tsDateUpdated`
- **THEN** the system excludes the record without using another date field as fallback

### Requirement: Candidate collection paginates in official latest-update order
The system SHALL request character-category pages in `_tsDateUpdated,DESC` order starting at page 1. For a finite range it SHALL continue until a page crosses the cutoff, the API reports completion, a page is empty, a later request fails, or a safety limit is reached.

#### Scenario: More than two pages are inside the window
- **WHEN** the first two pages and part or all of a later page contain records on or after the cutoff
- **THEN** the system continues beyond 100 records until a defined stop condition occurs

#### Scenario: Page crosses the cutoff
- **WHEN** a fetched page contains records both on or after and before the cutoff
- **THEN** the system retains the in-range records from that page and does not request another page

#### Scenario: Empty or complete page
- **WHEN** the API returns an empty page or reports that the response is complete
- **THEN** the system stops collection normally

#### Scenario: Later page fails
- **WHEN** page 1 succeeds and a later page fails
- **THEN** the system displays accumulated valid candidates and opens a non-blocking localized partial-results warning

### Requirement: Collection has hard safety limits
The system SHALL request no more than 10 pages and SHALL process no more than 500 raw records for one time-window load, including Unlimited. It SHALL stop as soon as either limit is reached.

#### Scenario: Page limit reached
- **WHEN** 10 pages have been processed and the API still indicates more results
- **THEN** the system returns the accumulated candidates without requesting page 11

#### Scenario: Record limit reached
- **WHEN** processing the next response would exceed 500 raw records
- **THEN** the system processes only the remaining allowed records and stops collection

### Requirement: Filtering and local ordering follow a stable sequence
The system SHALL merge pages and deduplicate records by mod ID before applying the time boundary, existing character-category constraint, NSFW setting, and submitter/mod-name blacklist. It SHALL apply local ordering only to the remaining records.

#### Scenario: Duplicate across page boundary
- **WHEN** the same mod ID appears on more than one fetched page
- **THEN** the system retains one candidate before content filtering and ordering

#### Scenario: Sort by latest update
- **WHEN** Latest Updated is selected
- **THEN** the system orders candidates by `DateUpdated` descending and then mod ID descending

#### Scenario: Sort by metric
- **WHEN** Most Liked, Most Downloaded, or Most Commented is selected
- **THEN** the system orders candidates by the selected metric descending, then `DateUpdated` descending, then mod ID descending

#### Scenario: Existing content filters remain active
- **WHEN** an in-window candidate is excluded by the NSFW setting or a blacklist rule
- **THEN** the system omits it before producing the final locally ordered card list

### Requirement: Compatible navigation restores time-window state
The system SHALL store the time range and local order in mod-list navigation entries and SHALL restore both after returning from a details page when the restored category, primary order, and search remain eligible. It SHALL update controls without issuing duplicate loads.

#### Scenario: Return from details
- **WHEN** the user opens a mod from a 90-day Most Downloaded list and returns
- **THEN** the system restores the 90-day range and Most Downloaded order and reloads the list once

#### Scenario: Restore incompatible state
- **WHEN** a saved time-window state is restored into an incompatible category, primary order, or search mode
- **THEN** the system normalizes the state and does not retain an invalid local order

### Requirement: User-facing text is localized
The system SHALL provide English, Simplified Chinese, and Traditional Chinese text for the time-range label, all range choices, the within-range sort label, all local sort choices, partial results, and safety-capped results. Other locales SHALL use the existing English fallback behavior.

#### Scenario: Supported locale
- **WHEN** the browser is displayed in English, Simplified Chinese, or Traditional Chinese
- **THEN** all new time-window controls and notices use native localized text

