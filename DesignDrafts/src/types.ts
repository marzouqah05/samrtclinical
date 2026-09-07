export type Language = 'ar' | 'en';

export type Role = 'admin' | 'doctor' | 'nurse';

export type NavTab = 'dashboard' | 'patients' | 'schedules' | 'records' | 'inventory' | 'analytics' | 'settings' | 'support';

export interface UserMember {
  id: string;
  name: string;
  nameEn: string;
  email: string;
  role: string;
  roleEn: string;
  department: string;
  departmentEn: string;
  status: 'active' | 'inactive';
  initials: string;
}

export interface QueueItem {
  id: string;
  patientName: string;
  patientNameEn: string;
  time: string;
  room: string;
  doctor: string;
  doctorEn: string;
  status: 'waiting' | 'in_consultation' | 'completed';
  waitingDuration?: string;
  waitingDurationEn?: string;
  checkoutTime?: string;
}

export interface Appointment {
  id: string;
  patientName: string;
  patientNameEn: string;
  type: string;
  typeEn: string;
  doctor: string;
  doctorEn: string;
  timeSlot: string; // e.g., "09:00 AM"
  status: 'scheduled' | 'in_progress' | 'completed';
  room?: string;
}

export interface MedicalFile {
  id: string;
  name: string;
  size: string;
  date: string;
  dateEn: string;
  type: 'image' | 'pdf';
  url: string;
}

export interface TimelineEntry {
  id: string;
  title: string;
  titleEn: string;
  time: string;
  timeEn: string;
  doctor: string;
  doctorEn: string;
  doctorAvatar?: string;
  content: string;
  contentEn: string;
  isSystem?: boolean;
}

export interface PatientData {
  id: string;
  mrn: string;
  name: string;
  nameEn: string;
  gender: string;
  genderEn: string;
  age: number;
  dob: string;
  dobEn: string;
  avatar: string;
  bp: string;
  hr: number;
  allergies: Array<{ name: string; nameEn: string; type: 'warning' | 'info' }>;
  nextApptDate: string;
  nextApptDateEn: string;
  nextApptDoctor: string;
  nextApptDoctorEn: string;
  status: 'active' | 'inactive';
  timeline: TimelineEntry[];
  files: MedicalFile[];
}

export interface BackupRecord {
  id: string;
  date: string;
  dateEn: string;
  size: string;
  status: 'success' | 'pending';
}

export interface ToastMessage {
  id: string;
  title: string;
  message: string;
  type: 'success' | 'info' | 'warning' | 'error';
}
