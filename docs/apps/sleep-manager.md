# Sleep Manager App

## Overview

The Sleep Manager app orchestrates bedtime and wake-up routines while monitoring energy prices and device battery levels. It integrates with the house state management system to create optimal sleep environments and provides intelligent notifications for energy savings and device maintenance.

## Key Features

### Sleep & Wake Routines
- **Bedtime Automation**: Automated sleep preparation routines
- **Wake-up Sequences**: Gentle wake-up lighting and device control
- **Sleep State Management**: Coordinates with house state system
- **Weekday Intelligence**: Prevents accidental sleep on work days

### Energy Price Monitoring
- **Real-time Price Tracking**: Monitors dynamic energy pricing
- **Price Alerts**: Notifications for significant price changes
- **Cost Optimization**: Alerts for energy-saving opportunities
- **Negative Price Alerts**: Notifications when energy prices go negative

### Battery Monitoring Integration
- **Device Battery Alerts**: Monitors phone and tablet battery levels
- **Low Battery Warnings**: Alerts when devices need charging
- **Sleep-time Charging**: Reminds to charge devices before sleep
- **Battery Threshold Management**: Configurable warning levels

### Roller Blind Control
- **Automated Blind Control**: Manages window coverings during sleep routines
- **Light Management**: Coordinates with lighting systems
- **Privacy Control**: Ensures privacy during sleep hours
- **Morning Light**: Gradual light introduction for natural wake-up

## Configuration

### Battery Monitoring Thresholds
- **Phone Battery**: Alert when below 30%
- **Tablet Battery**: Alert when below 30%
- **Check Frequency**: Regular monitoring intervals

### Energy Price Alerts
- **Significant Change**: Alerts for major price shifts
- **Negative Pricing**: Alerts when prices go negative
- **Optimization Opportunities**: Notifications for energy savings

### Sleep Schedule Settings
- **Weekday Protection**: Prevents sleep mode on work days
- **Weekend Flexibility**: Relaxed sleep scheduling
- **Holiday Modes**: Special schedules for holidays

## Technical Implementation

### Triggers
- **Sleep State Changes**: Boolean state transitions
- **Scheduled Wake-ups**: Time-based wake-up routines
- **Energy Price Changes**: Dynamic pricing alerts
- **Battery Level Changes**: Device battery monitoring
- **Weekday Detection**: Work day vs. leisure day logic

### Actions
- **Light Control**: Bedroom and ambient lighting management
- **Cover Control**: Roller blind automation
- **Device Notifications**: Battery and energy price alerts
- **State Coordination**: Integration with house state system

## Sleep Routines

### Bedtime Sequence
1. **Environment Preparation**
   - Dim bedroom lights gradually
   - Close roller blinds for privacy
   - Set house state to "Night"
   - Reduce ambient noise

2. **Device Management**
   - Check phone/tablet battery levels
   - Alert if charging needed
   - Adjust device settings for sleep mode

3. **Energy Optimization**
   - Check current energy prices
   - Provide energy-saving recommendations
   - Schedule high-energy tasks for off-peak hours

### Wake-up Sequence
1. **Gentle Lighting**
   - Gradual bedroom light increase
   - Warm to cool color temperature transition
   - Ambient lighting activation

2. **Environmental Control**
   - Open roller blinds gradually
   - Adjust room temperature
   - Prepare house for day activities

3. **Information Delivery**
   - Energy price updates
   - Battery status notifications
   - Weather and schedule information

## Energy Price Management

### Price Monitoring
- **Real-time Tracking**: Continuous energy price monitoring
- **Price History**: Tracks price trends and patterns
- **Threshold Alerts**: Notifications for significant price changes
- **Optimization Suggestions**: Recommendations for energy use timing

### Alert Types
- **High Price Alerts**: Warnings during expensive periods
- **Low Price Alerts**: Opportunities for energy-intensive tasks
- **Negative Price Alerts**: Rare negative pricing notifications
- **Price Spike Warnings**: Sudden price increase alerts

### Usage Optimization
- **Task Scheduling**: Recommendations for timing energy-intensive tasks
- **Device Control**: Automatic adjustment of non-essential devices
- **Cost Savings**: Notifications for potential savings opportunities
- **Historical Analysis**: Price pattern recognition for planning

## Battery Management

### Device Monitoring
- **Primary Devices**: Phone and tablet continuous monitoring
- **Secondary Devices**: Other battery-powered devices
- **Charging Reminders**: Timely charging notifications
- **Battery Health**: Long-term battery health tracking

### Alert System
- **Low Battery Warnings**: Immediate alerts when batteries are low
- **Charging Reminders**: Bedtime charging notifications
- **Full Charge Notifications**: Alerts when devices reach 100%
- **Battery Optimization**: Tips for extending battery life

## Integration

### Home Assistant Entities
- **Sleep Boolean**: Sleep state control
- **Energy Price Sensors**: Dynamic pricing data
- **Battery Sensors**: Device battery level monitoring
- **Cover Entities**: Roller blind control
- **Light Entities**: Bedroom and ambient lighting

### Connected Systems
- **House State Manager**: Coordinated state transitions
- **Lighting System**: Synchronized lighting control
- **Climate Control**: Temperature and humidity management
- **Notification System**: Multi-channel alert delivery

## Usage Examples

### Sleep Activation
```
🌙 Sleep Mode Activated
Time: 22:30
Actions:
- Bedroom lights dimmed to 10%
- Roller blinds closed
- House state set to Night
- Energy price: 0.12 €/kWh
```

### Battery Alert
```
🔋 Charge Before Sleep
Phone Battery: 25%
Tablet Battery: 18%
Recommendation: Charge overnight
Current time: 21:45
```

### Energy Price Alert
```
⚡ Energy Price Alert
Current: -0.05 €/kWh (Negative!)
Duration: Next 2 hours
Recommendation: Run dishwasher, washing machine
```

### Wake-up Sequence
```
☀️ Good Morning Wake-up
Time: 06:30
Actions:
- Bedroom lights at 40% warm white
- Roller blinds opening slowly
- Energy price: 0.08 €/kWh (Low)
- Phone battery: 100%
```

## Maintenance

### Regular Checks
- **Battery Sensor Accuracy**: Verify battery level reporting
- **Energy Price Data**: Check price feed connectivity
- **Light Control**: Test bedroom lighting responses
- **Cover Operation**: Verify roller blind functionality


### Optimization
- **Schedule Tuning**: Adjust wake-up times seasonally
- **Alert Thresholds**: Modify battery warning levels
- **Energy Settings**: Update price alert thresholds
- **Routine Customization**: Adapt routines to preferences

## Best Practices

### Sleep Hygiene
- **Consistent Schedule**: Maintain regular sleep patterns
- **Environment Control**: Optimize bedroom conditions
- **Device Management**: Minimize screen time before sleep
- **Gradual Transitions**: Smooth lighting and temperature changes

### Energy Efficiency
- **Price Awareness**: Monitor energy costs regularly
- **Task Timing**: Schedule energy-intensive tasks during low-price periods
- **Device Optimization**: Charge devices during off-peak hours
- **Consumption Tracking**: Monitor energy usage patterns

The Sleep Manager app creates a comprehensive sleep and energy management system that promotes healthy sleep patterns while optimizing energy costs and ensuring devices remain properly charged and functional.