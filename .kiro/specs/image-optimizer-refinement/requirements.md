# Requirements Document

## Introduction

This document specifies the requirements for refining and completing the Image Optimizer feature in FlairX Mod Manager. The Image Optimizer is a comprehensive system for processing mod and category preview images with various optimization modes, cropping strategies, and quality settings. The refinement focuses on verifying existing functionality, implementing missing features, fixing bugs, and ensuring all components work together cohesively across four optimization contexts: Manual, Drag & Drop (Mod), Drag & Drop (Category), and Automatic (GameBanana Download).

## Glossary

- **Image Optimizer**: The system responsible for processing, optimizing, and managing preview images for mods and categories
- **Optimization Mode**: A processing strategy that determines which operations are performed on images (Full, Lite, Rename, RenameOnly)
- **Full Mode**: Complete optimization including automatic cropping, quality conversion, resolution changes, JPEG encoding, and thumbnail generation
- **Lite Mode**: Optimization without resizing or cropping - only quality conversion and JPEG encoding
- **Rename Mode**: Generates standard thumbnails and renames files to manager-compatible names
- **RenameOnly Mode**: Only renames files to manager-compatible names without any processing
- **Cropping Strategy**: The algorithm used to determine crop area (Center, Smart, Entropy, Attention, ManualOnly)
- **Inspect and Edit Mode**: An optional setting that can be enabled with any automatic cropping strategy to allow user review and adjustment of automatically selected crop areas
- **Manual Optimization**: User-initiated optimization through the Image Optimizer page
- **Drag & Drop Mod**: Optimization triggered when user drags image files onto a mod
- **Drag & Drop Category**: Optimization triggered when user drags image files onto a category
- **Automatic Optimization**: Optimization triggered automatically when downloading mods from GameBanana
- **JPEG Quality**: Compression quality setting (60-100%) applied during image encoding
- **Thread Count**: Number of parallel processing threads for batch operations
- **Backup**: ZIP archive of original files created before optimization with timestamp
- **Keep Originals**: Setting to preserve original files after optimization instead of deleting them
- **Standard Names**: Manager-compatible file names (preview.jpg, minitile.jpg for mods; catprev.jpg, catmini.jpg for categories)
- **Minitile**: 600×722 thumbnail image for mod tiles with acrylic footer effect
- **Catmini**: 600×722 thumbnail image for category tiles with acrylic footer effect
- **Inspect and Edit**: Cropping mode where user can review and adjust automatically selected crop areas before processing
- **Sliding Panel**: Retractable UI panel that slides in from the side for focused interactions

## Requirements

### Requirement 1: Manual Optimization Settings Verification ✅ IMPLEMENTED

**User Story:** As a user performing manual optimization, I want all configured settings to be respected and applied correctly, so that images are processed according to my preferences.

#### Acceptance Criteria

1. ✅ WHEN a user sets JPEG quality to a specific value THEN the Image Optimizer SHALL encode all output images using that quality setting
2. ✅ WHEN a user sets thread count to a specific value greater than zero THEN the Image Optimizer SHALL use exactly that number of parallel threads during batch processing
3. ✅ WHEN a user sets thread count to zero THEN the Image Optimizer SHALL automatically detect logical CPU cores and use (cores - 1) threads
4. ✅ WHEN a user enables backup creation THEN the Image Optimizer SHALL create a timestamped ZIP archive of all original JPG and PNG files before processing
5. ✅ WHEN a user enables keep originals THEN the Image Optimizer SHALL preserve original files after optimization instead of deleting them
6. ✅ WHEN a user selects Full mode THEN the Image Optimizer SHALL perform cropping, resizing, quality conversion, and thumbnail generation exactly as it did before optimization mode options were added
7. ✅ WHEN a user selects Lite mode THEN the Image Optimizer SHALL perform quality conversion and JPEG encoding without resizing or cropping
8. ✅ WHEN a user selects Rename mode THEN the Image Optimizer SHALL generate thumbnails and rename files to standard names
9. ✅ WHEN a user selects RenameOnly mode THEN the Image Optimizer SHALL only rename files to standard names without processing

### Requirement 2: Drag & Drop Mod Optimization Settings Verification ✅ IMPLEMENTED

**User Story:** As a user dragging images onto mods, I want all configured settings to be respected and applied correctly, so that mod images are processed according to my preferences.

#### Acceptance Criteria

1. ✅ WHEN a user drags an image onto a mod THEN the Image Optimizer SHALL apply the configured JPEG quality setting
2. ✅ WHEN a user drags an image onto a mod THEN the Image Optimizer SHALL use the configured thread count for processing
3. ✅ WHEN backup creation is enabled and a user drags an image onto a mod THEN the Image Optimizer SHALL create a backup before processing
4. ✅ WHEN keep originals is enabled and a user drags an image onto a mod THEN the Image Optimizer SHALL preserve the original dragged file
5. ✅ WHEN Full mode is selected for drag & drop mod and a user drags an image THEN the Image Optimizer SHALL perform complete optimization with cropping and thumbnails
6. ✅ WHEN Lite mode is selected for drag & drop mod and a user drags an image THEN the Image Optimizer SHALL perform quality conversion and JPEG encoding without resizing or cropping
7. ✅ WHEN Rename mode is selected for drag & drop mod and a user drags an image THEN the Image Optimizer SHALL generate thumbnails and apply standard names
8. ✅ WHEN RenameOnly mode is selected for drag & drop mod and a user drags an image THEN the Image Optimizer SHALL only apply standard names

### Requirement 3: Drag & Drop Category Optimization Settings Verification ✅ IMPLEMENTED

**User Story:** As a user dragging images onto categories, I want the optimization to work correctly with proper file naming, so that category images display properly in the manager.

#### Acceptance Criteria

1. ✅ WHEN a user drags an image onto a category with Full mode selected THEN the Image Optimizer SHALL perform complete optimization including cropping, resizing, thumbnail generation, and apply standard category file names (catprev.jpg, catmini.jpg)
2. ✅ WHEN a user drags an image onto a category with RenameOnly mode selected THEN the Image Optimizer SHALL only rename files to catprev.jpg and catmini.jpg without any processing

### Requirement 4: Automatic GameBanana Download Optimization Settings Verification ✅ IMPLEMENTED

**User Story:** As a user downloading mods from GameBanana, I want automatic optimization to respect all configured settings, so that downloaded mod images are processed correctly without manual intervention unless I choose to adjust crop areas.

**Note:** Crop inspection is disabled for background processing (AllowUIInteraction = false) to avoid blocking downloads.

#### Acceptance Criteria

1. ✅ WHEN a mod is downloaded from GameBanana THEN the Image Optimizer SHALL apply the configured JPEG quality setting to all images
2. ✅ WHEN a mod is downloaded from GameBanana THEN the Image Optimizer SHALL use the configured thread count for batch processing
3. ✅ WHEN backup creation is enabled and a mod is downloaded THEN the Image Optimizer SHALL create a backup of original images
4. ✅ WHEN keep originals is enabled and a mod is downloaded THEN the Image Optimizer SHALL preserve original image files
5. ✅ WHEN Full mode is selected for automatic optimization THEN the Image Optimizer SHALL perform complete optimization on downloaded images
6. ✅ WHEN Lite mode is selected for automatic optimization THEN the Image Optimizer SHALL perform quality conversion and JPEG encoding without resizing or cropping
7. ✅ WHEN Rename mode is selected for automatic optimization THEN the Image Optimizer SHALL generate thumbnails and apply standard names
8. ✅ WHEN RenameOnly mode is selected for automatic optimization THEN the Image Optimizer SHALL only apply standard names to downloaded images

### Requirement 5: Cropping Strategy System Implementation ✅ IMPLEMENTED

**User Story:** As a user, I want to choose from multiple cropping strategies that work consistently across all optimization contexts, so that I can control how images are cropped to fit required dimensions.

#### Acceptance Criteria

1. ✅ WHEN a user selects Center cropping strategy THEN the Image Optimizer SHALL crop images from the center point
2. ✅ WHEN a user selects Smart cropping strategy THEN the Image Optimizer SHALL use intelligent content detection to determine optimal crop area
3. ✅ WHEN a user selects Entropy cropping strategy THEN the Image Optimizer SHALL analyze image entropy to find the most information-rich crop area
4. ✅ WHEN a user selects Attention cropping strategy THEN the Image Optimizer SHALL use attention detection algorithms to find visually important regions
5. ✅ WHEN a user selects ManualOnly cropping strategy THEN the Image Optimizer SHALL always present the crop inspection interface for every image
6. ✅ WHEN a cropping strategy is changed THEN the Image Optimizer SHALL apply the new strategy globally to all optimization contexts without affecting other functionality
7. ✅ WHEN any optimization mode requires cropping THEN the Image Optimizer SHALL use the currently selected cropping strategy

### Requirement 6: Crop Inspection and Editing Interface ✅ IMPLEMENTED

**User Story:** As a user, I want to optionally inspect and adjust automatically selected crop areas before processing, so that I can ensure important image content is preserved while maintaining required aspect ratios.

#### Acceptance Criteria

1. ✅ WHEN Inspect and Edit mode is enabled and an automatic cropping strategy (Center, Smart, Entropy, Attention) is selected THEN the Image Optimizer SHALL display a sliding panel showing the automatically determined crop area for user review and editing
2. ✅ WHEN Inspect and Edit mode is disabled and an automatic cropping strategy is selected THEN the Image Optimizer SHALL apply the cropping strategy directly without user interaction
3. ✅ WHEN the crop inspection interface is displayed THEN the Image Optimizer SHALL show the full image scaled to fit the panel regardless of dimensions
4. ✅ WHEN the crop inspection interface is displayed THEN the Image Optimizer SHALL show the automatically selected crop area as an overlay with adjustment handles
5. ✅ WHEN cropping a square image (preview.jpg, catprev.jpg) THEN the Image Optimizer SHALL maintain 1:1 aspect ratio during inspection and editing
6. ✅ WHEN cropping minitile.jpg or catmini.jpg THEN the Image Optimizer SHALL maintain the same aspect ratio as a 600×722 image during inspection and editing
7. ✅ WHEN a user drags corner handles in the crop interface THEN the Image Optimizer SHALL resize the crop area while maintaining the required aspect ratio
8. ✅ WHEN a user drags edge midpoint handles in the crop interface THEN the Image Optimizer SHALL resize the crop area without maintaining aspect ratio
9. ✅ WHEN a user drags the crop area body THEN the Image Optimizer SHALL move the entire crop area without changing its size
10. ✅ WHEN a user confirms the crop selection THEN the Image Optimizer SHALL proceed with optimization using the adjusted crop area
11. ✅ WHEN ManualOnly cropping strategy is selected THEN the Image Optimizer SHALL always display the crop inspection interface regardless of Inspect and Edit mode setting
12. ✅ WHEN ManualOnly cropping strategy is selected THEN the Image Optimizer SHALL use the same sliding panel interface as Inspect and Edit mode
13. ✅ WHEN Inspect and Edit mode is active THEN the Image Optimizer SHALL NOT modify the core optimization logic or file generation behavior
14. ✅ WHEN thread count setting is configured and either Inspect and Edit mode or ManualOnly strategy is active THEN the thread count setting SHALL be disabled because images must be processed sequentially for user review

### Requirement 7: Cropping Strategy Integration with Optimization Modes ✅ IMPLEMENTED

**User Story:** As a user, I want the selected cropping strategy to work seamlessly with all optimization modes, so that cropping behavior is consistent and predictable across all contexts.

#### Acceptance Criteria

1. ✅ WHEN Full mode is active and cropping is required THEN the Image Optimizer SHALL apply the selected cropping strategy
2. ✅ WHEN Lite mode is active THEN the Image Optimizer SHALL NOT perform any cropping regardless of cropping strategy
3. ✅ WHEN Rename mode is active and thumbnail generation requires cropping THEN the Image Optimizer SHALL apply the selected cropping strategy
4. ✅ WHEN RenameOnly mode is active THEN the Image Optimizer SHALL NOT perform any cropping regardless of cropping strategy
5. ✅ WHEN the cropping strategy is changed THEN the Image Optimizer SHALL use the new strategy for all subsequent operations without requiring application restart

### Requirement 8: User Interface Organization ✅ IMPLEMENTED

**User Story:** As a user, I want the Image Optimizer settings to be organized logically in the UI, so that I can easily find and configure related options.

#### Acceptance Criteria

1. ✅ WHEN a user navigates to the Image Optimizer page THEN the system SHALL display sections in this order: Manual Optimization, Quality Settings, Performance, Cropping, Optimization Modes, Backup and Cleanup
2. ✅ WHEN a user views the Quality Settings section THEN the system SHALL display JPEG quality slider with percentage indicator
3. ✅ WHEN a user views the Performance section THEN the system SHALL display thread count slider with detected logical CPU thread information
4. ✅ WHEN a user views the Cropping section THEN the system SHALL display cropping strategy selector (Center, Smart, Entropy, Attention, ManualOnly) and Inspect and Edit toggle option
5. ✅ WHEN a user views the Optimization Modes section THEN the system SHALL display mode selectors for Manual, Drag & Drop Mod, Drag & Drop Category, and GameBanana Download contexts
6. ✅ WHEN a user views the Backup and Cleanup section THEN the system SHALL display toggles for backup creation and keep originals options

### Requirement 9: Image Processing Correctness ✅ IMPLEMENTED

**User Story:** As a user, I want all image processing operations to produce correct output files with proper dimensions and quality, so that mods and categories display correctly in the manager.

#### Acceptance Criteria

1. ✅ WHEN Full mode processes a mod image THEN the Image Optimizer SHALL generate preview.jpg (and preview-01.jpg, preview-02.jpg, etc. for multiple images) and minitile.jpg with correct dimensions
2. ✅ WHEN Full mode processes a category image THEN the Image Optimizer SHALL generate catprev.jpg and catmini.jpg with correct dimensions
3. ✅ WHEN any mode generates minitile.jpg THEN the Image Optimizer SHALL create an image with dimensions 600×722 pixels
4. ✅ WHEN any mode generates catmini.jpg THEN the Image Optimizer SHALL create an image with dimensions 600×722 pixels
5. ✅ WHEN JPEG quality is set to a specific value THEN all generated JPEG files SHALL use that quality setting in their encoding parameters
6. ✅ WHEN cropping is performed THEN the Image Optimizer SHALL maintain the required aspect ratio for the target image type
7. ✅ WHEN backup creation is enabled THEN the backup ZIP file SHALL contain all original JPG and PNG files before any modifications
8. ✅ WHEN keep originals is disabled and optimization completes THEN the Image Optimizer SHALL delete original files that differ from generated files
9. ✅ WHEN keep originals is enabled and optimization completes THEN the Image Optimizer SHALL preserve all original files alongside generated files

### Requirement 10: Error Handling and Edge Cases ✅ IMPLEMENTED

**User Story:** As a user, I want the Image Optimizer to handle errors gracefully and provide clear feedback, so that I understand what went wrong and can take corrective action.

#### Acceptance Criteria

1. ✅ WHEN an image file cannot be read THEN the Image Optimizer SHALL log the error and continue processing remaining images
2. ✅ WHEN backup creation fails THEN the Image Optimizer SHALL abort optimization and notify the user
3. ⚠️ WHEN disk space is insufficient for optimization THEN the Image Optimizer SHALL detect the condition and notify the user before processing (Not implemented - low priority)
4. ⚠️ WHEN a user cancels optimization during processing THEN the Image Optimizer SHALL stop gracefully and restore any partial changes if backups exist (Partial - cancellation exists but restore not implemented)
5. ✅ WHEN the thread count slider is displayed THEN the Image Optimizer SHALL set the maximum value to the detected logical CPU thread count and prevent exceeding this limit
6. ✅ WHEN a user manually adjusts crop area THEN the Image Optimizer SHALL constrain the crop rectangle to stay within image dimensions and prevent exceeding image boundaries
7. ✅ WHEN crop inspection interface encounters an error THEN the Image Optimizer SHALL display an error message and allow the user to skip or retry
