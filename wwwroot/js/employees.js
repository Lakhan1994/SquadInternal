

    // Global variable to store current employee ID
    let currentEmployeeId = null;
   
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
        });
    }

    // Initialize score type indicator for add form
    document.addEventListener('DOMContentLoaded', function() {
        updateScoreType(eduScore, 'scoreTypeIndicator');
    });

    // Reset education when Add Employee modal opens
    const addEmployeeModal = document.getElementById("addEmployeeModal");
    if (addEmployeeModal) {
        addEmployeeModal.addEventListener("show.bs.modal", () => {
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
        });
    }

window.addEducation = function () {

    const level = document.getElementById("eduLevel")?.value || "";
    const degreeDiv = document.getElementById("degreeDiv");
    const degreeName = document.getElementById("degreeName");
    const institute = document.getElementById("eduInstitute")?.value.trim() || "";
    const year = document.getElementById("eduYear")?.value || "";
    const score = document.getElementById("eduScore")?.value || "";

    if (!level) {
        alert("Please select education level");
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

    const row = `
        <tr>
            <td>${escapeHtml(level)}</td>
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
    if (degreeName) degreeName.value = "";

    updateScoreType(document.getElementById("eduScore"), 'scoreTypeIndicator');
};



window.removeEducationRow = function (btn)
 {
        if (btn && btn.closest("tr")) {
            btn.closest("tr").remove();
        }
    }

    // Force submit Add Employee form
window.forceSubmitEmployeeForm = function ()
 {
        const form = document.getElementById("addEmployeeForm");
        if (form) form.submit();
    }

    /* =========================================================
       EDIT EMPLOYEE → SERVER-SIDE EDUCATION (API CALLS)
       ========================================================= */

    // Initialize edit modal education level toggle
    document.addEventListener('DOMContentLoaded', function() {
        const empEditEduLevel = document.getElementById("empEditEduLevel");
        const empEditDegreeDiv = document.getElementById("empEditDegreeDiv");

        if (empEditEduLevel && empEditDegreeDiv) {
            empEditEduLevel.addEventListener("change", function() {
                empEditDegreeDiv.classList.toggle(
                    "d-none",
                    !(this.value === "Degree" || this.value === "Post Graduation")
                );
            });
        }

        // Initialize edit education modal level toggle
        const editEduLevel = document.getElementById("editEduLevel");
        const editEduDegreeDiv = document.getElementById("editEduDegreeDiv");

        if (editEduLevel && editEduDegreeDiv) {
            editEduLevel.addEventListener("change", function() {
                editEduDegreeDiv.classList.toggle(
                    "d-none",
                    !(this.value === "Degree" || this.value === "Post Graduation")
                );
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

        if (empEditEduLevel) empEditEduLevel.value = "";
        if (empEditDegreeName) empEditDegreeName.value = "";
        if (empEditEduInstitute) empEditEduInstitute.value = "";
        if (empEditEduYear) empEditEduYear.value = "";
        if (empEditEduScore) empEditEduScore.value = "";
        if (empEditDegreeDiv) empEditDegreeDiv.classList.add("d-none");

        // Reset score indicator
        updateScoreType(empEditEduScore, 'empEditScoreTypeIndicator');
    }

    // Open Edit Employee modal
    function openEditEmployee(id, firstName, lastName, email, roleId, reportingToUserId, dateOfBirth, dateOfJoining, gender) {
        // Set current employee ID
        currentEmployeeId = id;

        // Set form values
        document.getElementById("editId").value = id;
        document.getElementById("editFirstName").value = firstName;
        document.getElementById("editLastName").value = lastName;
        document.getElementById("editEmail").value = email || '';
        document.getElementById("editRole").value = roleId || '';
        document.getElementById("editReportingToUserId").value = reportingToUserId || '';
        document.getElementById("editDateOfBirth").value = dateOfBirth || '';
        document.getElementById("editDateOfJoining").value = dateOfJoining || '';
        document.getElementById("editGender").value = gender || '';

        // Clear education form
        clearEditEducationForm();

        // Load education records
        loadEmployeeEducations(id);

        // Show modal
        const editModal = new bootstrap.Modal(document.getElementById("editEmployeeModal"));
        editModal.show();
    }

    // Add education to existing employee (for Edit modal)
    async function addEducationToEmployee() {
        console.log("addEducationToEmployee called");

        const employeeId = document.getElementById("editId")?.value;
        console.log("Employee ID:", employeeId);

        if (!employeeId) {
            alert("No employee selected. Please refresh and try again.");
            return;
        }

        const level = document.getElementById("empEditEduLevel")?.value;
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

        if (!institute || institute.trim() === "") {
            alert("Please enter institute name");
            return;
        }

        const data = {
            EmployeeId: parseInt(employeeId),
            Level: level,
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

    // Load employee educations
    async function loadEmployeeEducations(employeeId) {
        const tbody = document.getElementById("editEducationTable");
        if (!tbody) return;

        tbody.innerHTML = '<tr><td colspan="6" class="text-muted text-center">Loading...</td></tr>';

        try {
            const response = await fetch(`/Admin/GetEmployeeEducations?employeeId=${employeeId}`);

            if (!response.ok) {
                throw new Error(`Failed to load: ${response.status}`);
            }

            const data = await response.json();
            console.log("Education data received:", data);

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

                tbody.innerHTML += `
                <tr>
                    <td>${escapeHtml(e.level) || '-'}</td>
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
    async function openEditEducationById(id) {
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
    async function saveEducationEdit() {
        const id = document.getElementById("editEduId").value;
        const level = document.getElementById("editEduLevel").value;
        const degree = document.getElementById("editEduDegreeName")?.value || "";
        const institute = document.getElementById("editEduInstitute").value.trim();
        const year = document.getElementById("editEduYear").value;
        const score = document.getElementById("editEduScore").value;

        if (!level || !institute) {
            alert("Please fill all required fields");
            return;
        }

        const data = {
            Id: parseInt(id),
            Level: level,
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
    async function deleteEducation(id) {
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
        editEmployeeModal.addEventListener('show.bs.modal', function() {
            clearEditEducationForm();
        });
    }

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
        const toastHtml = `
            <div id="${toastId}" class="toast align-items-center text-bg-${type === 'success' ? 'success' : 'danger'} border-0" role="alert">
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
        toastElement.addEventListener('hidden.bs.toast', function() {
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
        return text.toString().replace(/[&<>"']/g, function(m) { return map[m]; });
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

