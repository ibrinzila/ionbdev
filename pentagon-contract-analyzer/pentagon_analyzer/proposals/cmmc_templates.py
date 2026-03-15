"""CMMC 2.0 compliance data for Level 1 and Level 2 templates."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass
class CMMCPractice:
    """A single CMMC practice/control."""

    id: str
    domain: str
    title: str
    description: str
    level: int  # 1 or 2
    nist_ref: str  # NIST SP 800-171 reference


# CMMC Level 1: 17 practices from FAR 52.204-21
LEVEL_1_PRACTICES = [
    CMMCPractice("AC.L1-b.1.i", "Access Control", "Authorized Access Control",
                 "Limit information system access to authorized users, processes acting on behalf of authorized users, or devices (including other information systems).",
                 1, "3.1.1"),
    CMMCPractice("AC.L1-b.1.ii", "Access Control", "Transaction & Function Control",
                 "Limit information system access to the types of transactions and functions that authorized users are permitted to execute.",
                 1, "3.1.2"),
    CMMCPractice("AC.L1-b.1.iii", "Access Control", "External Connections",
                 "Verify and control/limit connections to and use of external information systems.",
                 1, "3.1.20"),
    CMMCPractice("AC.L1-b.1.iv", "Access Control", "Control Public Information",
                 "Control information posted or processed on publicly accessible information systems.",
                 1, "3.1.22"),
    CMMCPractice("IA.L1-b.1.v", "Identification & Auth", "Identification",
                 "Identify information system users, processes acting on behalf of users, or devices.",
                 1, "3.5.1"),
    CMMCPractice("IA.L1-b.1.vi", "Identification & Auth", "Authentication",
                 "Authenticate (or verify) the identities of those users, processes, or devices, as a prerequisite to allowing access to organizational information systems.",
                 1, "3.5.2"),
    CMMCPractice("MP.L1-b.1.vii", "Media Protection", "Media Disposal",
                 "Sanitize or destroy information system media containing Federal Contract Information before disposal or release for reuse.",
                 1, "3.8.3"),
    CMMCPractice("PE.L1-b.1.viii", "Physical Protection", "Limit Physical Access",
                 "Limit physical access to organizational information systems, equipment, and the respective operating environments to authorized individuals.",
                 1, "3.10.1"),
    CMMCPractice("PE.L1-b.1.ix", "Physical Protection", "Escort Visitors",
                 "Escort visitors and monitor visitor activity.",
                 1, "3.10.3"),
    CMMCPractice("PE.L1-b.1.x", "Physical Protection", "Physical Access Logs",
                 "Maintain audit logs of physical access.",
                 1, "3.10.4"),
    CMMCPractice("PE.L1-b.1.xi", "Physical Protection", "Manage Physical Access",
                 "Control and manage physical access devices.",
                 1, "3.10.5"),
    CMMCPractice("SC.L1-b.1.xii", "System & Comms Protection", "Boundary Protection",
                 "Monitor, control, and protect organizational communications at the external boundaries and key internal boundaries of information systems.",
                 1, "3.13.1"),
    CMMCPractice("SC.L1-b.1.xiii", "System & Comms Protection", "Public Access Protections",
                 "Implement subnetworks for publicly accessible system components that are physically or logically separated from internal networks.",
                 1, "3.13.5"),
    CMMCPractice("SI.L1-b.1.xiv", "System & Info Integrity", "Flaw Remediation",
                 "Identify, report, and correct information and information system flaws in a timely manner.",
                 1, "3.14.1"),
    CMMCPractice("SI.L1-b.1.xv", "System & Info Integrity", "Malicious Code Protection",
                 "Provide protection from malicious code at appropriate locations within organizational information systems.",
                 1, "3.14.2"),
    CMMCPractice("SI.L1-b.1.xvi", "System & Info Integrity", "Update Malicious Code Protection",
                 "Update malicious code protection mechanisms when new releases are available.",
                 1, "3.14.4"),
    CMMCPractice("SI.L1-b.1.xvii", "System & Info Integrity", "System & File Scanning",
                 "Perform periodic scans of the information system and real-time scans of files from external sources as files are downloaded, opened, or executed.",
                 1, "3.14.5"),
]

# CMMC Level 2: 110 practices from NIST SP 800-171 Rev 2 (14 domains)
# These are the domain summaries - full 110 controls in templates
LEVEL_2_DOMAINS = [
    {"id": "AC", "name": "Access Control", "control_count": 22,
     "description": "Limit system access to authorized users and devices."},
    {"id": "AT", "name": "Awareness & Training", "control_count": 3,
     "description": "Ensure personnel are aware of security risks and trained in applicable policies."},
    {"id": "AU", "name": "Audit & Accountability", "control_count": 9,
     "description": "Create, protect, and retain system audit records."},
    {"id": "CM", "name": "Configuration Management", "control_count": 9,
     "description": "Establish and maintain baseline configurations and manage changes."},
    {"id": "IA", "name": "Identification & Authentication", "control_count": 11,
     "description": "Identify and authenticate system users, processes, and devices."},
    {"id": "IR", "name": "Incident Response", "control_count": 3,
     "description": "Establish incident handling capability including detection, analysis, containment, and recovery."},
    {"id": "MA", "name": "Maintenance", "control_count": 6,
     "description": "Perform timely maintenance on organizational systems."},
    {"id": "MP", "name": "Media Protection", "control_count": 9,
     "description": "Protect, sanitize, and control system media containing CUI."},
    {"id": "PE", "name": "Physical Protection", "control_count": 6,
     "description": "Limit physical access to systems and protect the physical plant."},
    {"id": "PS", "name": "Personnel Security", "control_count": 2,
     "description": "Screen individuals prior to access and ensure protection after termination/transfer."},
    {"id": "RA", "name": "Risk Assessment", "control_count": 3,
     "description": "Periodically assess risk to organizational operations and assets."},
    {"id": "CA", "name": "Security Assessment", "control_count": 4,
     "description": "Assess, monitor, and correct deficiencies in security controls."},
    {"id": "SC", "name": "System & Communications Protection", "control_count": 16,
     "description": "Monitor and protect communications at external and internal boundaries."},
    {"id": "SI", "name": "System & Information Integrity", "control_count": 7,
     "description": "Identify, report, and correct system flaws. Protect against malicious code."},
]
