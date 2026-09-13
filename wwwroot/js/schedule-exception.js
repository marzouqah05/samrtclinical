/**
 * schedule-exception.js
 * Dynamic Working Hours & Configurable Buffer Slot Validation with Exception Confirmation.
 */
(function (window) {
    'use strict';

    function timeToMinutes(tStr) {
        if (!tStr) return null;
        const parts = tStr.split(':');
        if (parts.length < 2) return null;
        return parseInt(parts[0], 10) * 60 + parseInt(parts[1], 10);
    }

    function minutesToTime(mins) {
        const h = Math.floor(mins / 60);
        const m = mins % 60;
        return String(h).padStart(2, '0') + ':' + String(m).padStart(2, '0');
    }

    function formatTimeDisplay(tStr) {
        if (!tStr) return '';
        const parts = tStr.split(':');
        if (parts.length < 2) return tStr;
        let h = parseInt(parts[0], 10);
        const m = parts[1];
        const ampm = h >= 12 ? 'PM' : 'AM';
        const h12 = h % 12 || 12;
        return `${String(h12).padStart(2, '0')}:${m} ${ampm}`;
    }

    window.initScheduleExceptionGuard = function (options) {
        const form = document.querySelector(options.formSelector);
        const timeInput = document.querySelector(options.timeInputSelector);
        const exceptionInput = document.querySelector(options.exceptionInputSelector);
        const dateInput = options.dateInputSelector ? document.querySelector(options.dateInputSelector) : null;
        const doctorSelect = options.doctorSelectSelector ? document.querySelector(options.doctorSelectSelector) : null;

        if (!form || !timeInput || !exceptionInput) return;

        let shiftStart = options.shiftStart || '08:00';
        let shiftEnd = options.shiftEnd || '20:00';
        let slotDuration = parseInt(options.slotDurationMinutes, 10) || 15;

        // Fetch refreshed config if doctor or date changes
        async function refreshConfig() {
            try {
                const docId = doctorSelect ? doctorSelect.value : '';
                const dateVal = dateInput ? dateInput.value : '';
                const resp = await fetch(`/Appointments/GetWorkingHoursConfig?doctorId=${encodeURIComponent(docId)}&date=${encodeURIComponent(dateVal)}`);
                if (resp.ok) {
                    const data = await resp.json();
                    if (data.shiftStart) shiftStart = data.shiftStart;
                    if (data.shiftEnd) shiftEnd = data.shiftEnd;
                    if (data.slotDurationMinutes) slotDuration = parseInt(data.slotDurationMinutes, 10);
                }
            } catch (e) {
                // Fallback to existing config
            }
        }

        if (doctorSelect) doctorSelect.addEventListener('change', refreshConfig);
        if (dateInput) dateInput.addEventListener('change', refreshConfig);

        timeInput.addEventListener('input', function () {
            exceptionInput.value = 'false';
        });
        timeInput.addEventListener('change', function () {
            exceptionInput.value = 'false';
        });

        form.addEventListener('submit', function (e) {
            if (exceptionInput.value === 'true') {
                // Already authorized as an exception
                return true;
            }

            const timeVal = timeInput.value;
            if (!timeVal) return true;

            const t = timeToMinutes(timeVal);
            const startM = timeToMinutes(shiftStart) || (8 * 60);
            const endM = timeToMinutes(shiftEnd) || (20 * 60);
            const opBufEndM = startM + slotDuration;
            const clBufStartM = endM - slotDuration;

            const opBufEndStr = minutesToTime(opBufEndM);
            const clBufStartStr = minutesToTime(clBufStartM);

            const isAr = document.documentElement.dir === 'rtl' || document.documentElement.lang.startsWith('ar');

            let isException = false;
            let msgEn = '';
            let msgAr = '';

            if (t < startM || t > endM) {
                isException = true;
                msgEn = `Selected time (${timeVal}) is outside the official working hours (${shiftStart} - ${shiftEnd}).`;
                msgAr = `الوقت المحدد (${timeVal}) يقع خارج ساعات العمل الرسمية للعيادة (${shiftStart} - ${shiftEnd}).`;
            } else if (t >= startM && t <= opBufEndM) {
                isException = true;
                msgEn = `Selected time (${timeVal}) is within the opening preparation slot (${shiftStart} - ${opBufEndStr}).`;
                msgAr = `الوقت المحدد (${timeVal}) يقع ضمن فترة الإعداد والتجهيز الافتتاحية (${shiftStart} - ${opBufEndStr}).`;
            } else if (t >= clBufStartM && t <= endM) {
                isException = true;
                msgEn = `Selected time (${timeVal}) is within the clinic closing wrap-up slot (${clBufStartStr} - ${shiftEnd}).`;
                msgAr = `الوقت المحدد (${timeVal}) يقع ضمن فترة الإغلاق وإنهاء العمل بالعيادة (${clBufStartStr} - ${shiftEnd}).`;
            }

            if (isException) {
                e.preventDefault();
                e.stopPropagation();

                const modalEl = document.getElementById('scheduleExceptionModal');
                if (!modalEl) {
                    // Fallback confirm dialog if modal DOM is absent
                    const confirmed = confirm((isAr ? msgAr : msgEn) + '\n\nAre you sure you want to book this appointment as an authorized exception?');
                    if (confirmed) {
                        exceptionInput.value = 'true';
                        form.submit();
                    }
                    return false;
                }

                const msgEl = document.getElementById('exceptionWarningMessage');
                if (msgEl) {
                    msgEl.innerHTML = `
                        <div>${isAr ? msgAr : msgEn}</div>
                        <div class="text-[11px] opacity-80 mt-1 font-normal">${isAr ? msgEn : msgAr}</div>
                    `;
                }

                const bsModal = bootstrap.Modal.getOrCreateInstance(modalEl);

                const btnConfirm = document.getElementById('btnExceptionConfirm');
                const btnChange = document.getElementById('btnExceptionChangeTime');

                function onConfirm() {
                    exceptionInput.value = 'true';
                    bsModal.hide();
                    cleanup();
                    // Submit guarded form
                    if (typeof form.requestSubmit === 'function') {
                        form.requestSubmit();
                    } else {
                        form.submit();
                    }
                }

                function onChange() {
                    bsModal.hide();
                    cleanup();
                    setTimeout(() => {
                        timeInput.focus();
                    }, 200);
                }

                function cleanup() {
                    if (btnConfirm) btnConfirm.removeEventListener('click', onConfirm);
                    if (btnChange) btnChange.removeEventListener('click', onChange);
                }

                if (btnConfirm) {
                    btnConfirm.replaceWith(btnConfirm.cloneNode(true));
                    const newBtnConfirm = document.getElementById('btnExceptionConfirm');
                    newBtnConfirm.addEventListener('click', onConfirm);
                }

                if (btnChange) {
                    btnChange.replaceWith(btnChange.cloneNode(true));
                    const newBtnChange = document.getElementById('btnExceptionChangeTime');
                    newBtnChange.addEventListener('click', onChange);
                }

                bsModal.show();
                return false;
            }

            return true;
        });
    };
})(window);
