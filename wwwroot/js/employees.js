// Ensure a global currentEmployeeId exists for inline onclick handlers
var currentEmployeeId = window.currentEmployeeId ?? null;

// Prevent this file from initializing multiple times (double-binding handlers)
if (window._employeesJsInitialized) {
    // already initialized, avoid re-binding events
    console.debug('employees.js: already initialized, skipping re-init');
} else {
    window._employeesJsInitialized = true;

    // Use the global currentEmployeeId variable (keeps inline handlers working)
    currentEmployeeId = currentEmployeeId ?? null;
    window.currentEmployeeId = currentEmployeeId;

    const maxYear = 2026; // Fixed maximum year

    /* =========================================================
       NEW HELPER FUNCTIONS FOR YEAR AND SCORE VALIDATION
       ========================================================= */

    // Detect if score is CGPA or Percentage and update indicator
    function updateScoreType(inputElement, indicatorId = 'scoreTypeIndicator') {
        if (!inputElement) return;

        const value = inputElement.value.trim();
        const indicator = document.getElementById(indicatorId);
        const helpText = document.getElementById(indicatorId.replace('Indicator', 'Help')) ||
            document.getElementById('scoreHelp') ||
            document.getElementById('editScoreHelp') ||
            document.getElementById('empEditScoreHelp');

        if (!value) {
            if (indicator) {
                indicator.textContent = '?';
                indicator.className = 'input-group-text';
            }
            if (helpText) {
                helpText.textContent = 'Enter score (auto-detected)';
                helpText.className = 'text-muted';
            }
            return;
        }

        // Remove any non-numeric characters except decimal point
        const cleanValue = value.replace(/[^\d.]/g, '');
        const numValue = parseFloat(cleanValue);

        if (isNaN(numValue)) {
            if (indicator) {
                indicator.textContent = '?';
                indicator.className = 'input-group-text';
            }
            if (helpText) {
                helpText.textContent = 'Invalid number';
                helpText.className = 'text-danger';
            }
            return;
        }

        // Determine if it's CGPA or Percentage
        // CGPA typically ranges 0-10, Percentage 0-100
        let isCGPA = false;
        let isValid = true;
        let message = '';

        if (numValue <= 10 && value.includes('.')) {
            // Likely CGPA if value ≤ 10 and has decimal
            isCGPA = true;
            if (numValue > 10) {
                isValid = false;
                message = 'CGPA should be ≤ 10';
            }
        } else if (numValue <= 100) {
            // Likely Percentage if value ≤ 100
            isCGPA = false;
            if (numValue > 100) {
                isValid = false;
                message = 'Percentage should be ≤ 100';
            }
        } else {
            // Default to Percentage for values > 10 and ≤ 1000
            isCGPA = false;
            if (numValue > 1000) {
                isValid = false;
                message = 'Score seems too high';
            }
        }

        // Update indicator
        if (indicator) {
            indicator.textContent = isCGPA ? 'CGPA' : '%';
            indicator.className = `input-group-text ${isValid ? 'text-white' : 'text-white'} ${isValid ? (isCGPA ? 'bg-info' : 'bg-success') : 'bg-danger'}`;
        }

        // Update help text
        if (helpText) {
            helpText.textContent = isValid ?
                (isCGPA ? 'CGPA (0-10 scale)' : 'Percentage (0-100)') :
                message;
            helpText.className = isValid ? 'text-muted' : 'text-danger';
        }

        return isCGPA;
    }

    // Format score for display with appropriate suffix
    function formatScore(score) {
        if (!score) return '-';

        const cleanScore = score.toString().replace(/[^\d.]/g, '');
        const numValue = parseFloat(cleanScore);

        if (isNaN(numValue)) return score;

        if (numValue <= 10 && score.includes('.')) {
            return `${score} (CGPA)`;
        } else if (numValue <= 100) {
            return `${score}%`;
        } else {
            return score;
        }
    }

    /* =========================================================
       ADD EMPLOYEE → CLIENT-SIDE EDUCATION (NO API CALLS)
       ========================================================= */

    const eduLevel = document.getElementById("eduLevel");
    const degreeDiv = document.getElementById("degreeDiv");
    const degreeName = document.getElementById("degreeName");
    const eduInstitute = document.getElementById("eduInstitute");
    const eduYear = document.getElementById("eduYear");
    const eduScore = document.getElementById("eduScore");

    let eduIndex = 0;

    // Toggle degree dropdown
    if (eduLevel) {
        eduLevel.addEventListener("change", () => {
            if (degreeDiv) {
                degreeDiv.classList.toggle(
                    "d-none",
                    !(eduLevel.value === "Degree" || eduLevel.value === "Post Graduation")
                );
            }
            const otherDiv = document.getElementById("eduOtherLevelDiv");
            if (otherDiv) {
                otherDiv.classList.toggle("d-none", eduLevel.value !== "Other");
            }
        });
    }

    // Initialize score type indicator for add form
    document.addEventListener('DOMContentLoaded', function () {
        updateScoreType(eduScore, 'scoreTypeIndicator');
    });

    // Reset education when Add Employee modal opens
    const addEmployeeModalElement = document.getElementById("addEmployeeModal");
    if (addEmployeeModalElement) {
        addEmployeeModalElement.addEventListener("show.bs.modal", () => {
            eduIndex = 0;
            const eduTableBody = document.getElementById("eduTableBody");
            if (eduTableBody) eduTableBody.innerHTML = "";
            if (eduLevel) eduLevel.value = "";
            if (degreeName) degreeName.value = "";
            if (eduInstitute) eduInstitute.value = "";
            if (eduYear) eduYear.value = "";
            if (eduScore) eduScore.value = "";
            if (degreeDiv) degreeDiv.classList.add("d-none");

            // Reset score indicator
            updateScoreType(eduScore, 'scoreTypeIndicator');

            // Reset documents
            resetAddEmployeeDocuments();
        });
    }

    /* =========================================================
       ADD EMPLOYEE DOCUMENTS - CLIENT-SIDE STORAGE
       ========================================================= */

    let addEmployeeDocuments = [];
    let docIndex = 0;

    function resetAddEmployeeDocuments() {
        addEmployeeDocuments = [];
        docIndex = 0;
        updateAddDocumentsTable();

        // Reset form
        document.getElementById("addDocumentType").value = "";
        document.getElementById("addDocumentFile").value = "";
        const otherDiv = document.getElementById("addDocTypeOtherDiv");
        if (otherDiv) {
            otherDiv.classList.add("d-none");
            document.getElementById("addDocTypeOther").value = "";
        }
    }

    window.addDocumentForNewEmployee = function () {
        const docType = document.getElementById("addDocumentType")?.value || "";
        const docTypeOther = document.getElementById("addDocTypeOther")?.value.trim() || "";
        const docFile = document.getElementById("addDocumentFile")?.files[0];

        if (!docType) {
            showToast("Please select a document type", "warning");
            return;
        }

        if (docType === "Other" && !docTypeOther) {
            showToast("Please specify the document type", "warning");
            return;
        }

        if (!docFile) {
            showToast("Please select a file", "warning");
            return;
        }

        // Check file size (5MB max)
        const maxSize = 5 * 1024 * 1024; // 5MB
        if (docFile.size > maxSize) {
            showToast("File size must be less than 5MB", "danger");
            return;
        }

        // Check file type
        const allowedTypes = ["application/pdf", "image/jpeg", "image/png", "image/webp"];
        if (!allowedTypes.includes(docFile.type)) {
            showToast("Only PDF, JPG, PNG or WEBP files are allowed", "danger");
            return;
        }

        // Add document to storage
        addEmployeeDocuments.push({
            docType: docType,
            docTypeOther: docType === "Other" ? docTypeOther : "",
            file: docFile,
            index: docIndex
        });

        docIndex++;
        updateAddDocumentsTable();

        // Reset form
        document.getElementById("addDocumentType").value = "";
        document.getElementById("addDocumentFile").value = "";
        const otherDiv = document.getElementById("addDocTypeOtherDiv");
        if (otherDiv) {
            otherDiv.classList.add("d-none");
            document.getElementById("addDocTypeOther").value = "";
        }

        showToast("Document added successfully", "success");
    };

    function updateAddDocumentsTable() {
        const tableBody = document.getElementById("addDocumentsTable");
        const noDocsRow = document.getElementById("noDocumentsRow");

        if (addEmployeeDocuments.length === 0) {
            tableBody.innerHTML = '<tr id="noDocumentsRow"><td colspan="4" class="text-center text-muted py-3">No documents added yet</td></tr>';
            return;
        }

        let html = "";
        addEmployeeDocuments.forEach((doc, idx) => {
            const displayType = doc.docType === "Other" ? doc.docTypeOther : doc.docType;
            const fileSize = (doc.file.size / 1024).toFixed(2); // Size in KB

            html += `
                <tr>
                    <td>${escapeHtml(displayType)}</td>
                    <td>${escapeHtml(doc.file.name)}</td>
                    <td>${fileSize} KB</td>
                    <td>
                        <button type="button" class="btn btn-sm btn-outline-danger" onclick="removeAddEmployeeDocument(${idx})">
                            <i class="bi bi-trash3"></i>
                        </button>
                    </td>
                </tr>
            `;
        });

        tableBody.innerHTML = html;
    }

    window.removeAddEmployeeDocument = function (index) {
        addEmployeeDocuments.splice(index, 1);
        updateAddDocumentsTable();
        showToast("Document removed", "info");
    };

    // Handle document type "Other" visibility
    document.addEventListener("change", function (e) {
        if (e.target && e.target.id === "addDocumentType") {
            const otherDiv = document.getElementById("addDocTypeOtherDiv");
            if (otherDiv) {
                otherDiv.classList.toggle("d-none", e.target.value !== "Other");
            }
        }
    });

    window.addEducation = function () {
        const level = document.getElementById("eduLevel")?.value || "";
        const otherLevel = document.getElementById("eduOtherLevel")?.value.trim() || "";
        const degreeDiv = document.getElementById("degreeDiv");
        const degreeName = document.getElementById("degreeName");
        const institute = document.getElementById("eduInstitute")?.value.trim() || "";
        const year = document.getElementById("eduYear")?.value || "";
        const score = document.getElementById("eduScore")?.value || "";

        if (!level) {
            alert("Please select education level");
            return;
        }

        if (level === "Other" && !otherLevel) {
            alert("Please specify the education level");
            return;
        }

        if (!institute) {
            alert("Please enter institute name");
            return;
        }

        const stream = degreeDiv && !degreeDiv.classList.contains("d-none")
            ? (degreeName ? degreeName.value : "")
            : "";

        const displayScore = formatScore(score);
        const displayLevel = level === "Other" ? otherLevel : level;

        const row = `
            <tr>
                <td>${escapeHtml(displayLevel)}</td>
                <td>${escapeHtml(stream) || "-"}</td>
                <td>${escapeHtml(institute)}</td>
                <td>${escapeHtml(year) || "-"}</td>
                <td>${displayScore}</td>
                <td class="text-nowrap">
                    <button type="button" class="btn btn-sm btn-outline-danger"
                            onclick="removeEducationRow(this)">
                        <i class="bi bi-trash3"></i>
                    </button>
                </td>

                <input type="hidden" name="Educations[${eduIndex}].Level" value="${escapeHtml(level)}" />
                <input type="hidden" name="Educations[${eduIndex}].LevelOther" value="${escapeHtml(otherLevel)}" />
                <input type="hidden" name="Educations[${eduIndex}].Stream" value="${escapeHtml(stream)}" />
                <input type="hidden" name="Educations[${eduIndex}].Institute" value="${escapeHtml(institute)}" />
                <input type="hidden" name="Educations[${eduIndex}].PassingYear" value="${escapeHtml(year)}" />
                <input type="hidden" name="Educations[${eduIndex}].PercentageOrCGPA" value="${escapeHtml(score)}" />
            </tr>`;

        document.getElementById("eduTableBody")
            ?.insertAdjacentHTML("beforeend", row);

        eduIndex++;

        document.getElementById("eduInstitute").value = "";
        document.getElementById("eduYear").value = "";
        document.getElementById("eduScore").value = "";
        const eduOtherLevelInput = document.getElementById("eduOtherLevel");
        if (eduOtherLevelInput) eduOtherLevelInput.value = "";
        const eduOtherLevelDiv = document.getElementById("eduOtherLevelDiv");
        if (eduOtherLevelDiv) eduOtherLevelDiv.classList.add("d-none");
        if (degreeName) degreeName.value = "";

        updateScoreType(document.getElementById("eduScore"), 'scoreTypeIndicator');
    };

    window.removeEducationRow = function (btn) {
        if (btn && btn.closest("tr")) {
            btn.closest("tr").remove();
        }
    }

    // Force submit Add Employee form with documents
    window.forceSubmitEmployeeForm = function () {
        const form = document.getElementById("addEmployeeForm");
        if (!form) return;

        // If there are documents to upload, use FormData
        if (addEmployeeDocuments && addEmployeeDocuments.length > 0) {
            const formData = new FormData(form);

            // Add documents to FormData
            addEmployeeDocuments.forEach((doc, idx) => {
                formData.append(`Documents[${idx}].DocumentType`, doc.docType);
                formData.append(`Documents[${idx}].DocumentTypeOther`, doc.docTypeOther);
                formData.append(`Documents[${idx}].File`, doc.file);
            });

            // Submit via fetch
            const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value;

            fetch(form.getAttribute("action"), {
                method: "POST",
                headers: {
                    "RequestVerificationToken": token || ""
                },
                body: formData
            })
                .then(response => {
                    if (response.ok) {
                        window.location.href = response.url || form.getAttribute("action").replace("CreateEmployee", "Employees");
                    } else {
                        return response.text().then(text => {
                            throw new Error("Form submission failed");
                        });
                    }
                })
                .catch(error => {
                    console.error("Error:", error);
                    showToast("Error submitting form. Please try again.", "danger");
                });
        } else {
            // No documents, submit normally
            form.submit();
        }
    }

    /* =========================================================
       TAB VALIDATION FOR ADD EMPLOYEE MODAL
       ========================================================= */

    // Track which tab user is currently on
    let currentAddEmployeeTab = 'personal';

    // Validate Personal Details tab
    function validatePersonalDetailsTab() {
        const firstName = document.querySelector('input[name="FirstName"]');
        const lastName = document.querySelector('input[name="LastName"]');
        const email = document.querySelector('input[name="Email"]');
        const roleId = document.querySelector('select[name="RoleId"]');

        if (!firstName || !firstName.value.trim()) {
            showToast('Please enter First Name', 'warning');
            return false;
        }

        if (!lastName || !lastName.value.trim()) {
            showToast('Please enter Last Name', 'warning');
            return false;
        }

        if (!email || !email.value.trim()) {
            showToast('Please enter Email', 'warning');
            return false;
        }

        // Basic email validation
        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        if (!emailRegex.test(email.value)) {
            showToast('Please enter a valid Email', 'warning');
            return false;
        }

        if (!roleId || !roleId.value) {
            showToast('Please select a Role', 'warning');
            return false;
        }

        return true;
    }

    // Setup tab switching with validation
    window.initAddEmployeeTabValidation = function () {
        const tabLinks = document.querySelectorAll('#addEmployeeForm .nav-link');

        tabLinks.forEach(link => {
            link.addEventListener('click', function (e) {
                // Get the target tab from href or data-bs-target
                let targetTab = this.getAttribute('data-bs-target')?.substring(1) // Remove # from data-bs-target
                    || this.getAttribute('href')?.substring(1); // Remove # from href

                // If trying to switch away from personal tab, validate first
                if (currentAddEmployeeTab === 'personal' && targetTab !== 'personal') {
                    if (!validatePersonalDetailsTab()) {
                        e.preventDefault();
                        return false;
                    }
                }

                // Update current tab
                currentAddEmployeeTab = targetTab || 'personal';
            });
        });
    };

    // Initialize when modal is shown
    const addEmployeeModal = document.getElementById('addEmployeeModal');
    if (addEmployeeModal) {
        addEmployeeModal.addEventListener('show.bs.modal', function () {
            setTimeout(initAddEmployeeTabValidation, 500);
        });
    }

    /* =========================================================
       EDIT EMPLOYEE → SERVER-SIDE EDUCATION (API CALLS)
       ========================================================= */

    // Initialize edit modal education level toggle
    document.addEventListener('DOMContentLoaded', function () {
        const empEditEduLevel = document.getElementById("empEditEduLevel");
        const empEditDegreeDiv = document.getElementById("empEditDegreeDiv");

        if (empEditEduLevel && empEditDegreeDiv) {
            empEditEduLevel.addEventListener("change", function () {
                empEditDegreeDiv.classList.toggle(
                    "d-none",
                    !(this.value === "Degree" || this.value === "Post Graduation")
                );
                const otherDiv = document.getElementById("empEditOtherLevelDiv");
                if (otherDiv) {
                    otherDiv.classList.toggle("d-none", this.value !== "Other");
                }
            });
        }

        // Initialize edit education modal level toggle
        const editEduLevel = document.getElementById("editEduLevel");
        const editEduDegreeDiv = document.getElementById("editEduDegreeDiv");

        if (editEduLevel && editEduDegreeDiv) {
            editEduLevel.addEventListener("change", function () {
                editEduDegreeDiv.classList.toggle(
                    "d-none",
                    !(this.value === "Degree" || this.value === "Post Graduation")
                );
                const otherDiv = document.getElementById("editEduOtherLevelDiv");
                if (otherDiv) {
                    otherDiv.classList.toggle("d-none", this.value !== "Other");
                }
            });
        }

        // Initialize score indicators
        const empEditScore = document.getElementById("empEditEduScore");
        if (empEditScore) {
            updateScoreType(empEditScore, 'empEditScoreTypeIndicator');
        }
    });

    function clearEditEducationForm() {
        const empEditEduLevel = document.getElementById("empEditEduLevel");
        const empEditDegreeName = document.getElementById("empEditDegreeName");
        const empEditEduInstitute = document.getElementById("empEditEduInstitute");
        const empEditEduYear = document.getElementById("empEditEduYear");
        const empEditEduScore = document.getElementById("empEditEduScore");
        const empEditDegreeDiv = document.getElementById("empEditDegreeDiv");
        const empEditOtherLevel = document.getElementById("empEditOtherLevel");
        const empEditOtherLevelDiv = document.getElementById("empEditOtherLevelDiv");

        if (empEditEduLevel) empEditEduLevel.value = "";
        if (empEditDegreeName) empEditDegreeName.value = "";
        if (empEditEduInstitute) empEditEduInstitute.value = "";
        if (empEditEduYear) empEditEduYear.value = "";
        if (empEditEduScore) empEditEduScore.value = "";
        if (empEditDegreeDiv) empEditDegreeDiv.classList.add("d-none");
        if (empEditOtherLevel) empEditOtherLevel.value = "";
        if (empEditOtherLevelDiv) empEditOtherLevelDiv.classList.add("d-none");

        // Reset score indicator
        updateScoreType(empEditEduScore, 'empEditScoreTypeIndicator');
    }

    // Open Edit Employee modal
    window.openEditEmployee = function (
        id,
        firstName,
        lastName,
        email,
        roleId,
        reportingToUserId,
        dateOfBirth,
        dateOfJoining,
        gender,
        hasProfilePicture,
        initials,
        mobileNumber,
        bloodGroup,
        salary
    ) {
        // ============================================================
        // 1. SET CURRENT EMPLOYEE
        // ============================================================
        currentEmployeeId = id;
        window.currentEmployeeId = id;

        // ============================================================
        // 2. RESET MODAL TO PERSONAL DETAILS TAB
        // ============================================================
        const editModalElement = document.getElementById("editEmployeeModal");

        if (!editModalElement) {
            console.error("Edit Employee modal not found.");
            return;
        }

        // Find all tab links
        const editTabLinks = editModalElement.querySelectorAll(
            '[data-bs-toggle="tab"]'
        );

        // Remove active state from every tab
        editTabLinks.forEach(tab => {
            tab.classList.remove("active");
            tab.setAttribute("aria-selected", "false");
        });

        // Hide every tab pane
        const editTabPanes = editModalElement.querySelectorAll(".tab-pane");

        editTabPanes.forEach(pane => {
            pane.classList.remove("show", "active");
        });

        // Activate Personal Details tab
        const personalTab = editModalElement.querySelector(
            '[data-bs-toggle="tab"][href="#edit-tab-personal"]'
        );

        const personalPane = editModalElement.querySelector(
            "#edit-tab-personal"
        );

        if (personalTab) {
            personalTab.classList.add("active");
            personalTab.setAttribute("aria-selected", "true");
        }

        if (personalPane) {
            personalPane.classList.add("show", "active");
        }

        // ============================================================
        // 3. CLEAR OLD EMPLOYEE EDUCATION DATA
        // ============================================================
        const educationTable =
            document.getElementById("editEducationTable");

        if (educationTable) {
            educationTable.innerHTML = `
            <tr>
                <td colspan="6" class="text-muted text-center">
                    Loading...
                </td>
            </tr>`;
        }

        // ============================================================
        // 4. CLEAR OLD DOCUMENT DATA
        // ============================================================
        const documentsBody =
            document.getElementById("editEmployeeDocumentsBody");

        if (documentsBody) {
            documentsBody.innerHTML = `
            <div class="text-center text-muted py-4">
                Select Documents tab to load documents.
            </div>`;
        }

        // ============================================================
        // 5. LOAD NEW EMPLOYEE PERSONAL DETAILS
        // ============================================================
        document.getElementById("editId").value = id;
        document.getElementById("editFirstName").value = firstName || "";
        document.getElementById("editLastName").value = lastName || "";
        document.getElementById("editEmail").value = email || "";
        document.getElementById("editRole").value = roleId || "";
        document.getElementById("editReportingToUserId").value =
            reportingToUserId || "";

        document.getElementById("editDateOfBirth").value =
            dateOfBirth || "";

        document.getElementById("editDateOfJoining").value =
            dateOfJoining || "";

        document.getElementById("editGender").value =
            gender || "";

        document.getElementById("editMobileNumber").value =
            mobileNumber || "";

        document.getElementById("editBloodGroup").value =
            bloodGroup || "";

        document.getElementById("editSalary").value =
            salary ?? "";

        // ============================================================
        // 6. RESET PROFILE PICTURE
        // ============================================================
        const picPreview =
            document.getElementById("editProfilePicturePreview");

        const picInitials =
            document.getElementById("editProfileInitials");

        const picFileInput =
            document.getElementById("editProfilePictureFile");

        if (picFileInput) {
            picFileInput.value = "";
        }

        if (hasProfilePicture === true || hasProfilePicture === "true") {

            if (picPreview) {
                picPreview.src =
                    `/Admin/GetProfilePicture?employeeId=${id}&t=${Date.now()}`;

                picPreview.style.display = "inline-block";
            }

            if (picInitials) {
                picInitials.style.display = "none";
            }

        } else {

            if (picPreview) {
                picPreview.src = "";
                picPreview.style.display = "none";
            }

            if (picInitials) {
                picInitials.style.display = "inline-flex";
                picInitials.textContent = initials || "";
            }
        }

        // ============================================================
        // 7. RESET EDUCATION FORM
        // ============================================================
        clearEditEducationForm();

        // ============================================================
        // 8. LOAD EDUCATION FOR CURRENT EMPLOYEE ONLY
        // ============================================================
        loadEmployeeEducations(id);

        // ============================================================
        // 9. SHOW MODAL
        // ============================================================
        const editModal =
            bootstrap.Modal.getOrCreateInstance(editModalElement);

        editModal.show();
    };

    // Upload/replace profile picture for the employee being edited
    async function uploadProfilePicture(file) {
        if (!currentEmployeeId || !file) return;

        const formData = new FormData();
        formData.append("employeeId", currentEmployeeId);
        formData.append("file", file);

        const tokenInput = document.querySelector('#editEmployeeForm input[name="__RequestVerificationToken"]');
        if (tokenInput) formData.append("__RequestVerificationToken", tokenInput.value);

        try {
            const response = await fetch("/Admin/UploadProfilePicture", {
                method: "POST",
                headers: { "X-Requested-With": "XMLHttpRequest" },
                body: formData
            });

            const result = await response.json();

            if (result.success) {
                const picPreview = document.getElementById("editProfilePicturePreview");
                const picInitials = document.getElementById("editProfileInitials");

                picPreview.src = `/Admin/GetProfilePicture?employeeId=${currentEmployeeId}&t=${new Date().getTime()}`;
                picPreview.style.display = 'inline-block';
                picInitials.style.display = 'none';

                if (typeof toastr !== 'undefined') {
                    toastr.success(result.message || "Profile picture updated successfully.");
                } else {
                    alert(result.message || "Profile picture updated successfully.");
                }

                // Refresh the row avatar in the list without a full reload, if present
                const rowImg = document.querySelector(`img.emp-avatar-img[data-employee-id="${currentEmployeeId}"]`);
                if (rowImg) {
                    rowImg.src = picPreview.src;
                }
            } else {
                if (typeof toastr !== 'undefined') {
                    toastr.error(result.message || "Failed to upload profile picture.");
                } else {
                    alert(result.message || "Failed to upload profile picture.");
                }
            }
        } catch (err) {
            console.error("Profile picture upload failed:", err);
            if (typeof toastr !== 'undefined') {
                toastr.error("Failed to upload profile picture.");
            } else {
                alert("Failed to upload profile picture.");
            }
        }
    }

    document.addEventListener("DOMContentLoaded", function () {
        const picFileInput = document.getElementById("editProfilePictureFile");
        if (picFileInput) {
            picFileInput.addEventListener("change", function () {
                if (this.files && this.files[0]) {
                    uploadProfilePicture(this.files[0]);
                }
            });
        }
    });

    // Add education to existing employee (for Edit modal)
    window.addEducationToEmployee = async function () {
        console.log("addEducationToEmployee called");

        const employeeId = document.getElementById("editId")?.value;
        console.log("Employee ID:", employeeId);

        if (!employeeId) {
            alert("No employee selected. Please refresh and try again.");
            return;
        }

        const level = document.getElementById("empEditEduLevel")?.value;
        const otherLevel = document.getElementById("empEditOtherLevel")?.value.trim() || "";
        const degree = document.getElementById("empEditDegreeName")?.value || "";
        const institute = document.getElementById("empEditEduInstitute")?.value;
        const year = document.getElementById("empEditEduYear")?.value;
        const score = document.getElementById("empEditEduScore")?.value;

        console.log("Form values:", { level, degree, institute, year, score });

        // Validation
        if (!level) {
            alert("Please select education level");
            return;
        }

        if (level === "Other" && !otherLevel) {
            alert("Please specify the education level");
            return;
        }

        if (!institute || institute.trim() === "") {
            alert("Please enter institute name");
            return;
        }

        const data = {
            EmployeeId: parseInt(employeeId),
            Level: level,
            LevelOther: otherLevel,
            Stream: degree,
            Institute: institute.trim(),
            PassingYear: year ? parseInt(year) : null,
            PercentageOrCGPA: score || null
        };

        console.log("Sending data:", data);

        // Get CSRF token
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        // Show loading state
        const addButton = document.querySelector('#edit-tab-education .btn-primary');
        const originalText = addButton.innerHTML;
        addButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Adding...';
        addButton.disabled = true;

        try {
            const response = await fetch('/Admin/AddEducation', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token || ''
                },
                body: JSON.stringify(data)
            });

            console.log("Response status:", response.status);

            if (!response.ok) {
                const errorText = await response.text();
                console.error("Server error response:", errorText);

                // Try to parse as JSON if possible
                try {
                    const errorJson = JSON.parse(errorText);
                    throw new Error(errorJson.message || `Server error: ${response.status}`);
                } catch (e) {
                    throw new Error(`Server error: ${response.status} - ${errorText}`);
                }
            }

            const result = await response.json();
            console.log("Response result:", result);

            if (result && result.success) {
                // Clear form
                clearEditEducationForm();

                // Refresh education table
                loadEmployeeEducations(employeeId);

                // Show success message
                showToast('Education added successfully', 'success');
            } else {
                const errorMsg = result?.message || 'Failed to add education. Please try again.';
                throw new Error(errorMsg);
            }
        } catch (error) {
            console.error('Error:', error);

            // Provide user-friendly error message
            let errorMessage = 'Failed to add education: ';

            if (error.message.includes('404')) {
                errorMessage += 'The server endpoint was not found. Please ensure the /Admin/AddEducation endpoint exists.';
            } else if (error.message.includes('500')) {
                errorMessage += 'Server error. Please contact administrator.';
            } else if (error.message.includes('Network')) {
                errorMessage += 'Network error. Please check your connection.';
            } else {
                errorMessage += error.message;
            }

            alert(errorMessage);
        } finally {
            // Reset button state
            addButton.innerHTML = originalText;
            addButton.disabled = false;
        }
    }

    // Simple in-flight / cache to avoid duplicate concurrent requests for same employee
    const _eduRequests = {};

    // Helper to render education rows into tbody
    function renderEducationRows(data, tbody) {
        tbody.innerHTML = "";

        if (!data || data.length === 0) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="6" class="text-muted text-center">
                        No education records
                    </td>
                </tr>`;
            return;
        }

        data.forEach(e => {
            // Format score for display
            const displayScore = formatScore(e.percentageOrCGPA);
            const displayLevel = e.level === "Other" && e.levelOther ? e.levelOther : e.level;

            tbody.innerHTML += `
            <tr>
                <td>${escapeHtml(displayLevel) || '-'}</td>
                <td>${escapeHtml(e.stream) || '-'}</td>
                <td>${escapeHtml(e.institute) || '-'}</td>
                <td>${escapeHtml(e.passingYear) || '-'}</td>
                <td>${displayScore}</td>
                <td class="text-nowrap">
                    <button type="button"
                            class="btn btn-sm btn-outline-primary me-1"
                            onclick="openEditEducationById(${e.id})">
                        <i class="bi bi-pencil"></i>
                    </button>
                    <button type="button" class="btn btn-sm btn-outline-danger" onclick="deleteEducation(${e.id})">
                        <i class="bi bi-trash"></i>
                    </button>
                </td>
            </tr>`;
        });
    }

    // Load employee educations
    async function loadEmployeeEducations(employeeId) {
        const tbody = document.getElementById("editEducationTable");
        if (!tbody) return;

        tbody.innerHTML = '<tr><td colspan="6" class="text-muted text-center">Loading...</td></tr>';

        try {
            // If there's already an in-flight request for this employee, reuse it
            if (_eduRequests[employeeId]) {
                const data = await _eduRequests[employeeId];
                console.log("Education data received (cached):", data);
                renderEducationRows(data, tbody);
                return;
            }

            _eduRequests[employeeId] = (async () => {
                const response = await fetch(`/Admin/GetEmployeeEducations?employeeId=${employeeId}`);

                if (!response.ok) {
                    throw new Error(`Failed to load: ${response.status}`);
                }

                const data = await response.json();
                return data;
            })();

            const data = await _eduRequests[employeeId];
            console.log("Education data received:", data);
            // clean up cache entry
            delete _eduRequests[employeeId];

            renderEducationRows(data, tbody);

            tbody.innerHTML = "";

            if (!data || data.length === 0) {
                tbody.innerHTML = `
                    <tr>
                        <td colspan="6" class="text-muted text-center">
                            No education records
                        </td>
                    </tr>`;
                return;
            }

            data.forEach(e => {
                // Format score for display
                const displayScore = formatScore(e.percentageOrCGPA);
                const displayLevel = e.level === "Other" && e.levelOther ? e.levelOther : e.level;

                tbody.innerHTML += `
                <tr>
                    <td>${escapeHtml(displayLevel) || '-'}</td>
                    <td>${escapeHtml(e.stream) || '-'}</td>
                    <td>${escapeHtml(e.institute) || '-'}</td>
                    <td>${escapeHtml(e.passingYear) || '-'}</td>
                    <td>${displayScore}</td>
                    <td class="text-nowrap">
                        <button type="button"
                                class="btn btn-sm btn-outline-primary me-1"
                                onclick="openEditEducationById(${e.id})">
                            <i class="bi bi-pencil"></i>
                        </button>
                        <button type="button"
                                class="btn btn-sm btn-outline-danger"
                                onclick="deleteEducation(${e.id})">
                            <i class="bi bi-trash3"></i>
                        </button>
                    </td>
                </tr>`;
            });
        } catch (error) {
            console.error('Error loading education:', error);
            tbody.innerHTML = `
                <tr>
                    <td colspan="6" class="text-danger text-center">
                        Failed to load education records: ${error.message}
                    </td>
                </tr>`;
        }
    }

    // Open edit education modal
    window.openEditEducationById = async function (id) {
        try {
            const response = await fetch(`/Admin/GetEducation/${id}`);

            if (!response.ok) {
                throw new Error('Failed to load education details');
            }

            const edu = await response.json();
            console.log("Education details loaded:", edu);
            openEditEducation(edu);
        } catch (error) {
            console.error('Error:', error);
            alert('Failed to load education details: ' + error.message);
        }
    }

    // Open edit education modal
    function openEditEducation(edu) {
        document.getElementById("editEduId").value = edu.id;
        document.getElementById("editEduLevel").value = edu.level;
        document.getElementById("editEduInstitute").value = edu.institute;
        document.getElementById("editEduYear").value = edu.passingYear || '';
        document.getElementById("editEduScore").value = edu.percentageOrCGPA || '';

        const editEduOtherLevel = document.getElementById("editEduOtherLevel");
        if (editEduOtherLevel) editEduOtherLevel.value = edu.levelOther || '';

        const editEduOtherLevelDiv = document.getElementById("editEduOtherLevelDiv");
        if (editEduOtherLevelDiv) {
            editEduOtherLevelDiv.classList.toggle("d-none", edu.level !== "Other");
        }

        // Handle degree/stream field
        const editEduDegreeName = document.getElementById("editEduDegreeName");
        if (editEduDegreeName && edu.stream) {
            editEduDegreeName.value = edu.stream;
        }

        // Show/hide degree dropdown based on level
        const editEduDegreeDiv = document.getElementById("editEduDegreeDiv");
        if (editEduDegreeDiv) {
            editEduDegreeDiv.classList.toggle(
                "d-none",
                !(edu.level === "Degree" || edu.level === "Post Graduation")
            );
        }

        // Update score type indicator
        setTimeout(() => {
            updateScoreType(document.getElementById("editEduScore"), 'editScoreTypeIndicator');
        }, 100);

        const editEduModal = new bootstrap.Modal(document.getElementById("editEducationModal"));
        editEduModal.show();
    }

    // Save edited education
    window.saveEducationEdit = async function () {
        const id = document.getElementById("editEduId").value;
        const level = document.getElementById("editEduLevel").value;
        const degree = document.getElementById("editEduDegreeName")?.value || "";
        const institute = document.getElementById("editEduInstitute").value.trim();
        const year = document.getElementById("editEduYear").value;
        const score = document.getElementById("editEduScore").value;
        const otherLevel = document.getElementById("editEduOtherLevel")?.value.trim() || "";

        if (!level || !institute) {
            alert("Please fill all required fields");
            return;
        }

        if (level === "Other" && !otherLevel) {
            alert("Please specify the education level");
            return;
        }

        const data = {
            Id: parseInt(id),
            Level: level,
            LevelOther: otherLevel,
            Stream: degree,
            Institute: institute,
            PassingYear: year ? parseInt(year) : null,
            PercentageOrCGPA: score || null
        };

        console.log("Saving education data:", data);

        // Get CSRF token
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        try {
            const response = await fetch('/Admin/UpdateEducation', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token || ''
                },
                body: JSON.stringify(data)
            });

            if (!response.ok) {
                throw new Error('Update failed');
            }

            const result = await response.json();

            // Close modal
            const editEduModal = bootstrap.Modal.getInstance(document.getElementById("editEducationModal"));
            if (editEduModal) editEduModal.hide();

            // Refresh education table
            if (currentEmployeeId) {
                loadEmployeeEducations(currentEmployeeId);
            }

            showToast('Education updated successfully', 'success');
        } catch (error) {
            console.error('Error:', error);
            alert('Failed to update education: ' + error.message);
        }
    }

    // Delete education
    window.deleteEducation = async function (id) {
        if (!confirm("Are you sure you want to delete this education record?")) return;

        // Get CSRF token
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        try {
            const response = await fetch(`/Admin/DeleteEducation/${id}`, {
                method: "DELETE",
                headers: {
                    'RequestVerificationToken': token || '',
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) {
                throw new Error('Delete failed');
            }

            const result = await response.json();

            // Refresh education table
            if (currentEmployeeId) {
                loadEmployeeEducations(currentEmployeeId);
            }

            showToast('Education deleted successfully', 'success');
        } catch (error) {
            console.error('Error:', error);

            let errorMessage = 'Failed to delete education: ';
            if (error.message.includes('JSON')) {
                errorMessage += 'Server returned invalid response. The endpoint may not exist.';
            } else {
                errorMessage += error.message;
            }

            alert(errorMessage);
        }
    }

    // Clear education form when edit modal opens
    const editEmployeeModal = document.getElementById('editEmployeeModal');
    if (editEmployeeModal) {
        editEmployeeModal.addEventListener('show.bs.modal', function () {
            clearEditEducationForm();
        });
    }

    /* =========================================================
       EDIT EMPLOYEE -> DOCUMENTS TAB
       ========================================================= */

    window.loadEmployeeDocuments = function (employeeId) {
        const body = document.getElementById('editEmployeeDocumentsBody');
        if (!body || !employeeId) return;

        body.innerHTML = '<div class="text-center text-muted py-4">Loading...</div>';

        fetch('/Admin/EmployeeDocuments?employeeId=' + encodeURIComponent(employeeId))
            .then(function (response) {
                if (!response.ok) throw new Error('Failed to load documents.');
                return response.text();
            })
            .then(function (html) {
                body.innerHTML = html;

                const docTypeSelect = body.querySelector('#docType');
                if (docTypeSelect) {
                    docTypeSelect.addEventListener('change', function () {
                        const otherDiv = body.querySelector('#docTypeOtherDiv');
                        if (otherDiv) {
                            otherDiv.classList.toggle('d-none', this.value !== 'Other');
                        }
                    });
                }
            })
            .catch(function () {
                body.innerHTML = '<div class="alert alert-danger">Unable to load documents.</div>';
            });
    };

    window.uploadEmployeeDocument = function () {
        const body = document.getElementById('editEmployeeDocumentsBody');
        if (!body) return;

        const employeeId = body.querySelector('#docEmployeeId')?.value;
        const documentType = body.querySelector('#docType')?.value;
        const documentTypeOther = body.querySelector('#docTypeOther')?.value.trim() || '';
        const fileInput = body.querySelector('#docFile');
        const file = fileInput?.files?.[0];
        const uploadBtn = body.querySelector('#docUploadBtn');

        if (!employeeId) {
            alert('No employee selected. Please refresh and try again.');
            return;
        }

        if (!documentType) {
            alert('Please select a document type.');
            return;
        }

        if (documentType === 'Other' && !documentTypeOther) {
            alert('Please specify the document type.');
            return;
        }

        if (!file) {
            alert('Please select a file to upload.');
            return;
        }

        const formData = new FormData();
        formData.append('employeeId', employeeId);
        formData.append('documentType', documentType);
        formData.append('documentTypeOther', documentTypeOther);
        formData.append('file', file);

        const token = body.querySelector('input[name="__RequestVerificationToken"]')?.value;

        const originalText = uploadBtn ? uploadBtn.innerHTML : '';
        if (uploadBtn) {
            uploadBtn.disabled = true;
            uploadBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span>';
        }

        fetch('/Admin/UploadEmployeeDocument', {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token || '',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: formData
        })
            .then(function (response) { return response.json(); })
            .then(function (result) {
                if (result && result.success) {
                    showToast(result.message || 'Document uploaded successfully', 'success');
                    loadEmployeeDocuments(employeeId);
                } else {
                    alert(result?.message || 'Failed to upload document.');
                }
            })
            .catch(function () {
                alert('Failed to upload document.');
            })
            .finally(function () {
                if (uploadBtn) {
                    uploadBtn.disabled = false;
                    uploadBtn.innerHTML = originalText;
                }
            });
    };

    window.deleteEmployeeDocument = function (id, employeeId) {
        if (!confirm('Are you sure you want to delete this document?')) return;

        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        fetch('/Admin/DeleteEmployeeDocument', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': token || '',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: 'id=' + encodeURIComponent(id) + '&employeeId=' + encodeURIComponent(employeeId)
        })
            .then(function (response) { return response.json(); })
            .then(function (result) {
                if (result && result.success) {
                    showToast(result.message || 'Document deleted successfully', 'success');
                    loadEmployeeDocuments(employeeId);
                } else {
                    alert(result?.message || 'Failed to delete document.');
                }
            })
            .catch(function () {
                alert('Failed to delete document.');
            });
    };

    // Helper function to show toast notifications
    function showToast(message, type = 'info') {
        // Create toast container if it doesn't exist
        let toastContainer = document.getElementById('toast-container');
        if (!toastContainer) {
            toastContainer = document.createElement('div');
            toastContainer.id = 'toast-container';
            toastContainer.className = 'toast-container position-fixed bottom-0 end-0 p-3';
            document.body.appendChild(toastContainer);
        }

        const toastId = 'toast-' + Date.now();

        // Determine toast background color based on type
        let bgClass = 'text-bg-danger';
        if (type === 'success') bgClass = 'text-bg-success';
        else if (type === 'warning') bgClass = 'text-bg-warning';
        else if (type === 'info') bgClass = 'text-bg-info';

        const toastHtml = `
            <div id="${toastId}" class="toast align-items-center ${bgClass} border-0" role="alert">
                <div class="d-flex">
                    <div class="toast-body">
                        ${escapeHtml(message)}
                    </div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
                </div>
            </div>
        `;

        toastContainer.insertAdjacentHTML('beforeend', toastHtml);

        const toastElement = document.getElementById(toastId);
        const toast = new bootstrap.Toast(toastElement);
        toast.show();

        // Remove toast after it hides
        toastElement.addEventListener('hidden.bs.toast', function () {
            toastElement.remove();
        });
    }

    // Helper function to escape HTML
    function escapeHtml(text) {
        if (!text) return '';
        const map = {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#039;'
        };
        return text.toString().replace(/[&<>"']/g, function (m) { return map[m]; });
    }

    document.addEventListener("change", function (e) {
        if (e.target && e.target.id === "eduLevel") {
            const degreeDiv = document.getElementById("degreeDiv");
            if (!degreeDiv) return;

            if (e.target.value === "Degree" || e.target.value === "Post Graduation") {
                degreeDiv.classList.remove("d-none");
            } else {
                degreeDiv.classList.add("d-none");
            }
        }
    });
}