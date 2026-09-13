import React, { createContext, useContext, useState, useEffect } from 'react';
import {
  Language,
  Role,
  NavTab,
  UserMember,
  QueueItem,
  Appointment,
  PatientData,
  BackupRecord,
  ToastMessage,
  MedicalFile,
  TimelineEntry,
} from '../types';
import {
  INITIAL_USERS,
  INITIAL_QUEUE,
  INITIAL_APPOINTMENTS,
  PATIENT_OMAR,
  INITIAL_BACKUPS,
} from '../data/initialData';

interface AppContextType {
  lang: Language;
  setLang: (lang: Language) => void;
  toggleLang: () => void;
  isAuthenticated: boolean;
  userRole: Role;
  login: (role: Role, email?: string) => void;
  logout: () => void;
  activeTab: NavTab;
  setActiveTab: (tab: NavTab) => void;
  users: UserMember[];
  addUser: (user: Omit<UserMember, 'id' | 'initials'>) => void;
  queue: QueueItem[];
  updateQueueStatus: (id: string, status: 'waiting' | 'in_consultation' | 'completed') => void;
  appointments: Appointment[];
  addAppointment: (appt: Omit<Appointment, 'id'>) => void;
  patient: PatientData;
  updatePatient: (data: Partial<PatientData>) => void;
  addPatientFile: (file: Omit<MedicalFile, 'id'>) => void;
  addTimelineEntry: (entry: Omit<TimelineEntry, 'id'>) => void;
  backups: BackupRecord[];
  isSyncing: boolean;
  triggerForceSync: () => void;
  downloadSnapshot: () => void;
  downloadFullArchive: () => void;
  toasts: ToastMessage[];
  addToast: (title: string, message: string, type?: ToastMessage['type']) => void;
  removeToast: (id: string) => void;
  // Modal states
  isApptModalOpen: boolean;
  setIsApptModalOpen: (open: boolean) => void;
  isUserModalOpen: boolean;
  setIsUserModalOpen: (open: boolean) => void;
  previewFile: MedicalFile | null;
  setPreviewFile: (file: MedicalFile | null) => void;
  selectedClinic: string;
  setSelectedClinic: (clinic: string) => void;
}

const AppContext = createContext<AppContextType | undefined>(undefined);

export const AppProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [lang, setLangState] = useState<Language>('ar');
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(true);
  const [userRole, setUserRole] = useState<Role>('admin');
  const [activeTab, setActiveTab] = useState<NavTab>('dashboard');

  const [users, setUsers] = useState<UserMember[]>(INITIAL_USERS);
  const [queue, setQueue] = useState<QueueItem[]>(INITIAL_QUEUE);
  const [appointments, setAppointments] = useState<Appointment[]>(INITIAL_APPOINTMENTS);
  const [patient, setPatient] = useState<PatientData>(PATIENT_OMAR);
  const [backups, setBackups] = useState<BackupRecord[]>(INITIAL_BACKUPS);
  const [isSyncing, setIsSyncing] = useState<boolean>(false);
  const [toasts, setToasts] = useState<ToastMessage[]>([]);
  const [selectedClinic, setSelectedClinic] = useState<string>('المركز الطبي المركزي');

  // Modals
  const [isApptModalOpen, setIsApptModalOpen] = useState(false);
  const [isUserModalOpen, setIsUserModalOpen] = useState(false);
  const [previewFile, setPreviewFile] = useState<MedicalFile | null>(null);

  const setLang = (newLang: Language) => {
    setLangState(newLang);
    document.documentElement.dir = newLang === 'ar' ? 'rtl' : 'ltr';
    document.documentElement.lang = newLang;
    if (newLang === 'ar') {
      setSelectedClinic('المركز الطبي المركزي');
    } else {
      setSelectedClinic('Central Medical');
    }
  };

  const toggleLang = () => {
    setLang(lang === 'ar' ? 'en' : 'ar');
  };

  useEffect(() => {
    document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
    document.documentElement.lang = lang;
  }, [lang]);

  const addToast = (title: string, message: string, type: ToastMessage['type'] = 'success') => {
    const id = Math.random().toString(36).substring(2, 9);
    setToasts((prev) => [...prev, { id, title, message, type }]);
    setTimeout(() => {
      removeToast(id);
    }, 4000);
  };

  const removeToast = (id: string) => {
    setToasts((prev) => prev.filter((t) => t.id !== id));
  };

  const login = (role: Role, email?: string) => {
    setUserRole(role);
    setIsAuthenticated(true);
    addToast(
      lang === 'ar' ? 'تم تسجيل الدخول بنجاح' : 'Logged in successfully',
      lang === 'ar'
        ? `مرحباً بك كـ ${role === 'admin' ? 'مسؤول' : role === 'doctor' ? 'طبيب' : 'ممرض'}`
        : `Welcome back as ${role}`,
      'success'
    );
  };

  const logout = () => {
    setIsAuthenticated(false);
    addToast(
      lang === 'ar' ? 'تم تسجيل الخروج' : 'Logged out',
      lang === 'ar' ? 'نراك قريباً في ClinicFlow' : 'See you soon on ClinicFlow',
      'info'
    );
  };

  const addUser = (userData: Omit<UserMember, 'id' | 'initials'>) => {
    const initials = userData.nameEn
      .split(' ')
      .map((n) => n[0])
      .join('')
      .substring(0, 2)
      .toUpperCase() || 'ST';

    const newUser: UserMember = {
      ...userData,
      id: `u-${Date.now()}`,
      initials,
    };
    setUsers((prev) => [newUser, ...prev]);
    addToast(
      lang === 'ar' ? 'تمت إضافة المستخدم' : 'User Added',
      lang === 'ar' ? `تم تسجيل ${userData.name} بنجاح` : `${userData.nameEn} registered successfully`,
      'success'
    );
  };

  const updateQueueStatus = (id: string, status: 'waiting' | 'in_consultation' | 'completed') => {
    setQueue((prev) =>
      prev.map((item) => {
        if (item.id === id) {
          const now = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
          return {
            ...item,
            status,
            checkoutTime: status === 'completed' ? now : item.checkoutTime,
          };
        }
        return item;
      })
    );
    addToast(
      lang === 'ar' ? 'تم تحديث قائمة الانتظار' : 'Queue Updated',
      lang === 'ar' ? 'تم تحديث حالة المريض' : 'Patient status updated',
      'info'
    );
  };

  const addAppointment = (apptData: Omit<Appointment, 'id'>) => {
    const newAppt: Appointment = {
      ...apptData,
      id: `a-${Date.now()}`,
    };
    setAppointments((prev) => [newAppt, ...prev]);

    // Also add to patient queue if scheduled for today
    const newQueueItem: QueueItem = {
      id: `q-${Date.now()}`,
      patientName: apptData.patientName,
      patientNameEn: apptData.patientNameEn,
      time: apptData.timeSlot,
      room: apptData.room || 'غرفة A',
      doctor: apptData.doctor,
      doctorEn: apptData.doctorEn,
      status: 'waiting',
      waitingDuration: 'قيد الانتظار (جديد)',
      waitingDurationEn: 'Waiting (New)',
    };
    setQueue((prev) => [newQueueItem, ...prev]);

    addToast(
      lang === 'ar' ? 'تم حجز الموعد' : 'Appointment Booked',
      lang === 'ar'
        ? `تم حجز موعد لـ ${apptData.patientName} في ${apptData.timeSlot}`
        : `Appointment booked for ${apptData.patientNameEn} at ${apptData.timeSlot}`,
      'success'
    );
  };

  const addPatientFile = (fileData: Omit<MedicalFile, 'id'>) => {
    const newFile: MedicalFile = {
      ...fileData,
      id: `f-${Date.now()}`,
    };
    setPatient((prev) => ({
      ...prev,
      files: [newFile, ...prev.files],
    }));
    addToast(
      lang === 'ar' ? 'تم رفع الملف' : 'File Uploaded',
      lang === 'ar' ? `تمت إضافة الملف ${fileData.name}` : `Added file ${fileData.name}`,
      'success'
    );
  };

  const addTimelineEntry = (entryData: Omit<TimelineEntry, 'id'>) => {
    const newEntry: TimelineEntry = {
      ...entryData,
      id: `t-${Date.now()}`,
    };
    setPatient((prev) => ({
      ...prev,
      timeline: [newEntry, ...prev.timeline],
    }));
    addToast(
      lang === 'ar' ? 'تمت إضافة الملاحظة السريرية' : 'Clinical Note Added',
      lang === 'ar' ? 'تم حفظ التقرير في الملف الطبي للمريض' : 'Saved report to patient medical file',
      'success'
    );
  };

  const updatePatient = (data: Partial<PatientData>) => {
    setPatient((prev) => ({
      ...prev,
      ...data,
    }));
    addToast(
      lang === 'ar' ? 'تم تحديث بيانات المريض' : 'Patient Updated',
      lang === 'ar' ? 'تم حفظ التعديلات بنجاح' : 'Changes saved successfully',
      'success'
    );
  };

  const triggerForceSync = () => {
    setIsSyncing(true);
    addToast(
      lang === 'ar' ? 'جاري المزامنة السحابية...' : 'Syncing with cloud...',
      lang === 'ar' ? 'جاري الاتصال بـ Google Drive وتحديث النسخة' : 'Connecting to Google Drive and updating backup',
      'info'
    );

    setTimeout(() => {
      setIsSyncing(false);
      const newBackup: BackupRecord = {
        id: `b-${Date.now()}`,
        date: lang === 'ar' ? 'الآن، لقطة فورية' : 'Just now, Snapshot',
        dateEn: 'Just now, Snapshot',
        size: '2.4GB',
        status: 'success',
      };
      setBackups((prev) => [newBackup, ...prev]);
      addToast(
        lang === 'ar' ? 'تمت المزامنة بنجاح' : 'Sync Complete',
        lang === 'ar' ? 'تم حفظ أحدث لقطة بيانات في Google Drive' : 'Latest snapshot backed up to Google Drive',
        'success'
      );
    }, 1500);
  };

  const downloadSnapshot = () => {
    addToast(
      lang === 'ar' ? 'جاري تنزيل لقطة اليوم' : 'Downloading Snapshot',
      lang === 'ar' ? 'حجم الملف 2.4GB - ClinicFlow_Snapshot_2026.enc' : 'File size 2.4GB - ClinicFlow_Snapshot_2026.enc',
      'success'
    );
  };

  const downloadFullArchive = () => {
    addToast(
      lang === 'ar' ? 'جاري إعداد الأرشيف الكامل' : 'Preparing Full Archive',
      lang === 'ar' ? 'تم إنشاء حزمة الأرشيف الكاملة المشفرة' : 'Encrypted full archive package created',
      'info'
    );
  };

  return (
    <AppContext.Provider
      value={{
        lang,
        setLang,
        toggleLang,
        isAuthenticated,
        userRole,
        login,
        logout,
        activeTab,
        setActiveTab,
        users,
        addUser,
        queue,
        updateQueueStatus,
        appointments,
        addAppointment,
        patient,
        updatePatient,
        addPatientFile,
        addTimelineEntry,
        backups,
        isSyncing,
        triggerForceSync,
        downloadSnapshot,
        downloadFullArchive,
        toasts,
        addToast,
        removeToast,
        isApptModalOpen,
        setIsApptModalOpen,
        isUserModalOpen,
        setIsUserModalOpen,
        previewFile,
        setPreviewFile,
        selectedClinic,
        setSelectedClinic,
      }}
    >
      {children}
    </AppContext.Provider>
  );
};

export const useApp = () => {
  const context = useContext(AppContext);
  if (!context) {
    throw new Error('useApp must be used within an AppProvider');
  }
  return context;
};
