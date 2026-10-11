import { defineEventHandler } from 'h3'

export default defineEventHandler(() => {
  return {
    apiTitle: 'Heimdall IT Automation & Ticketing Integration API',
    version: 'v1',
    description: 'RESTful API for external IT systems (ServiceNow, Jira Service Management, Ansible, PowerShell) to manage directory security group mappings, pre-flight evaluate user claims, and auto-link ticketing change records.',
    authScheme: {
      type: 'apiKey',
      headerName: 'X-API-Key',
      alternativeHeader: 'Authorization: Bearer <key>',
      description: 'Generate keys on the Heimdall dashboard under /dashboard/security-groups.'
    },
    endpoints: [
      {
        path: '/api/v1/automation/security-groups/mappings',
        method: 'GET',
        requiredScope: 'security_groups:read',
        description: 'List configured directory security group mappings with optional filters (role, identityProvider, organizationId).'
      },
      {
        path: '/api/v1/automation/security-groups/mappings',
        method: 'POST',
        requiredScope: 'security_groups:write',
        description: 'Register or update a security group mapping from a ticketing workflow, auto-creating a governance audit trail ticket.',
        samplePayload: {
          identityProvider: 'EntraID',
          groupIdentifier: '9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c',
          displayName: 'OT Plant Battery Line Engineers',
          mappedRole: 'controls_engineer',
          organizationId: 'Line 06 – Battery Module Line',
          ticketId: 'JIRA-4819',
          ticketUrl: 'https://jira.corp.local/browse/JIRA-4819',
          requestedBy: 'sally.vance@plant.corp',
          approvedBy: 'lead.it.admin@plant.corp',
          reason: 'Emergency temporary delegation for line commissioning',
          autoCreateAuditTicket: true
        }
      },
      {
        path: '/api/v1/automation/security-groups/mappings/{id}',
        method: 'PATCH',
        requiredScope: 'security_groups:write',
        description: 'Update an existing mapping status, role, or boundary.'
      },
      {
        path: '/api/v1/automation/security-groups/mappings/{id}',
        method: 'DELETE',
        requiredScope: 'security_groups:write',
        description: 'Deprovision or delete a directory group mapping.'
      },
      {
        path: '/api/v1/automation/security-groups/evaluate',
        method: 'POST',
        requiredScope: 'security_groups:evaluate',
        description: 'Pre-flight check: simulate user directory group IDs/DNs to preview resolved roles and tenant organizations.',
        samplePayload: {
          groupIdentifiers: [
            'CN=OT-Controls-Engineers,OU=Groups,DC=factory,DC=corp',
            '9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c'
          ],
          ticketId: 'INC-10928',
          userId: 'usr_49182'
        }
      },
      {
        path: '/api/v1/automation/security-groups/sync',
        method: 'POST',
        requiredScope: 'security_groups:write',
        description: 'Trigger organization enrollment and role sync for a user in Heimdall.'
      }
    ],
    examples: {
      curl: `curl -X POST https://heimdall.plant.corp/api/v1/automation/security-groups/mappings \\
  -H "X-API-Key: hmd_auto_your_key_here" \\
  -H "Content-Type: application/json" \\
  -d '{
    "identityProvider": "EntraID",
    "groupIdentifier": "9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c",
    "displayName": "OT Plant Battery Line Engineers",
    "mappedRole": "controls_engineer",
    "organizationId": "Line 06 – Battery Module Line",
    "ticketId": "CHG0030192",
    "requestedBy": "engineer@plant.corp",
    "reason": "Change Request CHG0030192 approved for battery line access"
  }'`,
      serviceNow: `// ServiceNow Scripted REST Message / Flow Designer Action
var request = new sn_ws.RESTMessageV2();
request.setEndpoint('https://heimdall.plant.corp/api/v1/automation/security-groups/mappings');
request.setHttpMethod('POST');
request.setRequestHeader('X-API-Key', 'hmd_auto_your_key_here');
request.setRequestHeader('Content-Type', 'application/json');

var payload = {
  identityProvider: 'ActiveDirectory',
  groupIdentifier: 'CN=OT-Controls-Engineers,OU=Groups,DC=factory,DC=corp',
  displayName: 'Controls Engineers',
  mappedRole: 'engineer',
  ticketId: current.getValue('number'), // e.g. RITM0010023
  requestedBy: current.requested_for.getDisplayValue(),
  reason: current.getValue('short_description')
};

request.setRequestBody(JSON.stringify(payload));
var response = request.execute();`,
      jiraWebhook: `// Jira Automation Rule -> Send Web Request
URL: https://heimdall.plant.corp/api/v1/automation/security-groups/mappings
Method: POST
Headers:
  X-API-Key: hmd_auto_your_key_here
  Content-Type: application/json
Custom data:
{
  "identityProvider": "EntraID",
  "groupIdentifier": "{{issue.customfield_10020}}",
  "displayName": "{{issue.summary}}",
  "mappedRole": "{{issue.customfield_10021}}",
  "ticketId": "{{issue.key}}",
  "requestedBy": "{{issue.reporter.emailAddress}}",
  "reason": "{{issue.description}}"
}`
    }
  }
})
