import 'package:equatable/equatable.dart';

/// Modelo imutável representando o horário e informações de uma celebração ou reunião regular.
class MeetingScheduleModel extends Equatable {
  final String? id;
  final int dayOfWeek;
  final String startTime;
  final String description;
  final String language;

  const MeetingScheduleModel({
    this.id,
    required this.dayOfWeek,
    required this.startTime,
    required this.description,
    this.language = 'pt',
  });

  factory MeetingScheduleModel.fromJson(Map<String, dynamic> json) {
    return MeetingScheduleModel(
      id: json['id'] as String?,
      dayOfWeek: (json['dayOfWeek'] as num?)?.toInt() ?? 0,
      startTime: json['startTime'] as String? ?? '',
      description: json['description'] as String? ?? '',
      language: json['language'] as String? ?? 'pt',
    );
  }

  Map<String, dynamic> toJson() {
    final map = <String, dynamic>{
      'dayOfWeek': dayOfWeek,
      'startTime': startTime,
      'description': description,
      'language': language,
    };
    if (id != null) {
      map['id'] = id;
    }
    return map;
  }

  MeetingScheduleModel copyWith({
    String? id,
    int? dayOfWeek,
    String? startTime,
    String? description,
    String? language,
  }) {
    return MeetingScheduleModel(
      id: id ?? this.id,
      dayOfWeek: dayOfWeek ?? this.dayOfWeek,
      startTime: startTime ?? this.startTime,
      description: description ?? this.description,
      language: language ?? this.language,
    );
  }

  @override
  List<Object?> get props => [id, dayOfWeek, startTime, description, language];
}
